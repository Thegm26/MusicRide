using UnityEngine;

namespace MusicRoad
{
    [RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
    public sealed class ArcadeCarController : MonoBehaviour
    {
        private const float NormalMaxSpeed = 30f;
        private const float NitroMaxSpeed = 44f;
        private Rigidbody body;
        private RoadGenerator road;
        private Vector3 safePosition;
        private Quaternion safeRotation;
        private float offRoadTime;
        private float safePointTimer;
        private bool grounded;
        private bool onRoad;
        private bool jumpRequested;
        private bool boosting;

        public float SpeedKph => body == null ? 0f : body.linearVelocity.magnitude * 3.6f;
        public bool IsOnRoad => onRoad;
        public bool IsBoosting => boosting;

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.R) && body != null)
            {
                ResetToSafePoint();
            }

            if (Input.GetKeyDown(KeyCode.Space))
            {
                jumpRequested = true;
            }
        }

        public void Initialize(RoadGenerator roadGenerator)
        {
            road = roadGenerator;
            body = GetComponent<Rigidbody>();
            body.mass = 650f;
            body.linearDamping = 0.12f;
            body.angularDamping = 4f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.Continuous;
            body.centerOfMass = new Vector3(0f, -0.45f, 0f);
        }

        public void PlaceAtStart()
        {
            transform.SetPositionAndRotation(road.GetStartPosition(), road.GetStartRotation());
            safePosition = transform.position;
            safeRotation = transform.rotation;
            body.position = transform.position;
            body.rotation = transform.rotation;
        }

        private void FixedUpdate()
        {
            if (body == null || road == null)
            {
                return;
            }

            float throttle = Input.GetAxisRaw("Vertical");
            float steering = Input.GetAxisRaw("Horizontal");
            bool nitro = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            boosting = nitro && throttle > 0.1f;

            grounded = Physics.Raycast(transform.position + transform.up * 0.25f, -transform.up, out RaycastHit hit, 1.4f);
            road.TryGetRoadInfo(transform.position, out Vector3 roadPoint, out Vector3 roadTangent, out float lateralDistance);
            onRoad = lateralDistance <= RoadGenerator.RoadHalfWidth + 0.35f;

            if (grounded)
            {
                ApplySuspensionAndAlignment(hit, roadTangent);
            }

            ApplySteering(steering);

            if (grounded)
            {
                ApplyDrive(throttle, boosting);
                if (jumpRequested)
                {
                    body.AddForce(hit.normal * 14f + transform.forward * 1.2f, ForceMode.VelocityChange);
                }
            }
            else
            {
                body.AddForce(Physics.gravity * 0.8f, ForceMode.Acceleration);
                body.AddForce(transform.forward * (throttle * (boosting ? 7f : 3.5f)), ForceMode.Acceleration);
            }

            jumpRequested = false;
            UpdateRecovery(roadPoint, roadTangent);
        }

        private void ApplySuspensionAndAlignment(RaycastHit hit, Vector3 roadTangent)
        {
            float suspensionCompression = Mathf.Clamp01((1.05f - hit.distance) / 0.65f);
            body.AddForce(hit.normal * (suspensionCompression * 24f - Vector3.Dot(body.linearVelocity, hit.normal) * 4.5f), ForceMode.Acceleration);

            Vector3 desiredForward = Vector3.ProjectOnPlane(transform.forward, hit.normal).normalized;
            if (desiredForward.sqrMagnitude < 0.1f)
            {
                desiredForward = Vector3.ProjectOnPlane(roadTangent, hit.normal).normalized;
            }

            Quaternion desiredRotation = Quaternion.LookRotation(desiredForward, hit.normal);
            body.MoveRotation(Quaternion.Slerp(body.rotation, desiredRotation, Time.fixedDeltaTime * 6f));
        }

        private void ApplySteering(float steering)
        {
            Vector3 localVelocity = transform.InverseTransformDirection(body.linearVelocity);
            float speed = body.linearVelocity.magnitude;
            float speedRatio = Mathf.Clamp01(speed / NormalMaxSpeed);
            float movement = Mathf.InverseLerp(0.2f, 3f, Mathf.Abs(localVelocity.z));
            float direction = Mathf.Abs(localVelocity.z) > 0.2f ? Mathf.Sign(localVelocity.z) : 1f;
            float turnRate = Mathf.Lerp(95f, 62f, speedRatio);
            float yaw = steering * direction * turnRate * movement * Time.fixedDeltaTime;
            Quaternion steeringRotation = Quaternion.AngleAxis(yaw, transform.up);
            body.MoveRotation(steeringRotation * body.rotation);
        }

        private void ApplyDrive(float throttle, bool useNitro)
        {
            Vector3 localVelocity = transform.InverseTransformDirection(body.linearVelocity);
            float speed = body.linearVelocity.magnitude;
            float roadGrip = onRoad ? 1f : 0.42f;

            float speedLimit = useNitro ? NitroMaxSpeed : NormalMaxSpeed;
            if (speed < speedLimit || Mathf.Sign(throttle) != Mathf.Sign(localVelocity.z))
            {
                float acceleration = throttle >= 0f
                    ? useNitro ? 34f : 18f
                    : 11f;
                body.AddForce(transform.forward * (throttle * acceleration * roadGrip), ForceMode.Acceleration);
            }

            Vector3 lateralVelocity = transform.right * localVelocity.x;
            body.AddForce(-lateralVelocity * (onRoad ? 7.5f : 2.1f), ForceMode.Acceleration);

            if (!onRoad)
            {
                body.AddForce(-body.linearVelocity * 0.8f, ForceMode.Acceleration);
            }
        }

        private void UpdateRecovery(Vector3 roadPoint, Vector3 roadTangent)
        {
            if (onRoad && grounded)
            {
                offRoadTime = 0f;
                safePointTimer += Time.fixedDeltaTime;
                if (safePointTimer >= 0.8f)
                {
                    safePointTimer = 0f;
                    safePosition = roadPoint + Vector3.up * 0.9f;
                    safeRotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(roadTangent, Vector3.up).normalized, Vector3.up);
                }
            }
            else
            {
                offRoadTime += Time.fixedDeltaTime;
                if (offRoadTime > 3f || transform.position.y < roadPoint.y - 8f)
                {
                    ResetToSafePoint();
                }
            }
        }

        public void ResetToSafePoint()
        {
            offRoadTime = 0f;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.position = safePosition;
            body.rotation = safeRotation;
            RunManager.Instance?.BreakCombo();
        }
    }
}
