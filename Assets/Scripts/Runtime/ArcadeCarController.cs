using UnityEngine;

namespace MusicRoad
{
    [RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
    public sealed class ArcadeCarController : MonoBehaviour
    {
        private const float NormalMaxSpeed = 30f;
        private const float NitroMaxSpeed = 56f;
        private Rigidbody body;
        private RoadGenerator road;
        private Transform[] nitroFlames;
        private float offRoadTime;
        private bool grounded;
        private bool onRoad;
        private bool jumpRequested;
        private bool boosting;
        private bool wasBoosting;

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

        public void ConfigureNitroFlames(Transform[] flames)
        {
            nitroFlames = flames;
            for (int i = 0; i < nitroFlames.Length; i++)
            {
                nitroFlames[i].gameObject.SetActive(false);
            }
        }

        public void PlaceAtStart()
        {
            transform.SetPositionAndRotation(road.GetStartPosition(), road.GetStartRotation());
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
            if (boosting && !wasBoosting)
            {
                body.AddForce(transform.forward * 4.5f, ForceMode.VelocityChange);
            }
            UpdateNitroFlames();

            grounded = Physics.Raycast(transform.position + transform.up * 0.25f, -transform.up, out RaycastHit hit, 1.4f);
            bool nearGeneratedRoad = road.TryGetRoadInfo(transform.position, out Vector3 roadPoint, out Vector3 roadTangent, out float lateralDistance);
            onRoad = nearGeneratedRoad && lateralDistance <= RoadGenerator.RoadHalfWidth + 0.35f;

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
                body.AddForce(transform.forward * (throttle * (boosting ? 14f : 3.5f)), ForceMode.Acceleration);
            }

            jumpRequested = false;
            wasBoosting = boosting;
            UpdateRecovery(roadPoint);
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
                    ? useNitro ? 58f : 18f
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

        private void UpdateNitroFlames()
        {
            if (nitroFlames == null)
            {
                return;
            }

            float pulse = 1f + Mathf.Sin(Time.time * 38f) * 0.18f;
            for (int i = 0; i < nitroFlames.Length; i++)
            {
                Transform flame = nitroFlames[i];
                if (flame.gameObject.activeSelf != boosting)
                {
                    flame.gameObject.SetActive(boosting);
                }

                if (boosting)
                {
                    flame.localScale = new Vector3(0.9f, 0.9f, pulse);
                }
            }
        }

        private void UpdateRecovery(Vector3 roadPoint)
        {
            if (onRoad && grounded)
            {
                offRoadTime = 0f;
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
            if (road.TryGetClosestRoadPose(transform.position, out Vector3 roadPoint, out Vector3 roadTangent))
            {
                Vector3 recoveryPosition = roadPoint + Vector3.up * 1.35f;
                Quaternion recoveryRotation = Quaternion.LookRotation(roadTangent, Vector3.up);
                transform.SetPositionAndRotation(recoveryPosition, recoveryRotation);
                body.position = recoveryPosition;
                body.rotation = recoveryRotation;
            }
            else
            {
                PlaceAtStart();
            }
            RunManager.Instance?.BreakCombo();
        }
    }
}
