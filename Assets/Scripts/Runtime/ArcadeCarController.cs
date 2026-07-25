using UnityEngine;
using UnityEngine.EventSystems;

namespace MusicRoad
{
    [RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
    public sealed class ArcadeCarController : MonoBehaviour
    {
        private Rigidbody body;
        private RoadGenerator road;
        private Transform[] nitroFlames;
        private string vehicleName = "TOY CAR";
        private bool canBoost = true;
        private float normalMaxSpeed = 30f;
        private float nitroMaxSpeed = 56f;
        private float forwardAcceleration = 18f;
        private float nitroAcceleration = 58f;
        private float reverseAcceleration = 11f;
        private float lowSpeedTurnRate = 95f;
        private float highSpeedTurnRate = 62f;
        private float vehicleMass = 650f;
        private bool grounded;
        private bool onRoad;
        private bool jumpRequested;
        private bool frontflipRequested;
        private bool frontflipActive;
        private bool boosting;
        private bool wasBoosting;
        private float frontflipDegreesRemaining;
        private Vector3 frontflipAxis;

        public float SpeedKph => body == null ? 0f : body.linearVelocity.magnitude * 3.6f;
        public bool IsGrounded => grounded;
        public bool IsFrontflipping => frontflipActive;
        public bool IsOnRoad => onRoad;
        public bool IsBoosting => boosting;
        public bool CanBoost => canBoost;
        public string VehicleName => vehicleName;

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

            if (
                Input.GetMouseButtonDown(0) &&
                (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject()))
            {
                frontflipRequested = true;
            }
        }

        public void Initialize(RoadGenerator roadGenerator)
        {
            road = roadGenerator;
            body = GetComponent<Rigidbody>();
            body.mass = vehicleMass;
            body.linearDamping = 0.12f;
            body.angularDamping = 4f;
            body.maxAngularVelocity = 24f;
            body.useGravity = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.Continuous;
            body.centerOfMass = new Vector3(0f, -0.45f, 0f);
        }

        public void ConfigureVehicle(VehicleSpec spec)
        {
            vehicleName = spec.DisplayName;
            canBoost = spec.CanNitro;
            normalMaxSpeed = spec.MaxSpeed;
            nitroMaxSpeed = spec.NitroMaxSpeed;
            forwardAcceleration = spec.ForwardAcceleration;
            nitroAcceleration = spec.NitroAcceleration;
            reverseAcceleration = spec.ReverseAcceleration;
            lowSpeedTurnRate = spec.LowSpeedTurnRate;
            highSpeedTurnRate = spec.HighSpeedTurnRate;
            vehicleMass = spec.Mass;
            if (body != null)
            {
                body.mass = vehicleMass;
            }
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

            UpdateFrontflip();

            float throttle = Input.GetAxisRaw("Vertical");
            float steering = Input.GetAxisRaw("Horizontal");
            bool nitro = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            boosting = canBoost && nitro && throttle > 0.1f;
            if (boosting && !wasBoosting)
            {
                body.AddForce(transform.forward * 4.5f, ForceMode.VelocityChange);
            }
            UpdateNitroFlames();

            grounded = Physics.Raycast(
                transform.position + transform.up * 0.25f,
                -transform.up,
                out RaycastHit hit,
                1.4f);
            bool nearGeneratedRoad = road.TryGetRoadInfo(
                transform.position,
                out _,
                out Vector3 roadTangent,
                out float lateralDistance);
            onRoad = nearGeneratedRoad && lateralDistance <= RoadGenerator.RoadHalfWidth + 0.35f;
            if (grounded && !frontflipActive)
            {
                ApplySuspensionAndAlignment(hit, roadTangent);
            }

            if (!frontflipActive)
            {
                ApplySteering(steering);
            }

            if (grounded)
            {
                ApplyDrive(throttle, boosting);
                if (frontflipRequested)
                {
                    BeginFrontflip(hit.normal, true);
                }
                else if (jumpRequested)
                {
                    body.AddForce(hit.normal * 14f + transform.forward * 1.2f, ForceMode.VelocityChange);
                }
            }
            else
            {
                body.AddForce(transform.forward * (throttle * (boosting ? 22f : 9f)), ForceMode.Acceleration);
                if (frontflipRequested)
                {
                    BeginFrontflip(Vector3.up, false);
                }
            }

            jumpRequested = false;
            frontflipRequested = false;
            wasBoosting = boosting;
        }

        private void BeginFrontflip(Vector3 launchNormal, bool launch)
        {
            frontflipAxis = transform.right.normalized;
            frontflipDegreesRemaining = 360f;
            frontflipActive = true;
            body.angularDamping = 0f;
            body.angularVelocity = Vector3.ProjectOnPlane(body.angularVelocity, frontflipAxis);
            if (launch)
            {
                Vector3 planarForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
                body.AddForce(
                    launchNormal * 8.8f + planarForward * 2.4f,
                    ForceMode.VelocityChange);
            }
        }

        private void UpdateFrontflip()
        {
            if (!frontflipActive)
            {
                return;
            }

            float rotationStep = Mathf.Min(
                frontflipDegreesRemaining,
                1080f * Time.fixedDeltaTime);
            body.angularVelocity = Vector3.ProjectOnPlane(body.angularVelocity, frontflipAxis);
            body.MoveRotation(
                Quaternion.AngleAxis(rotationStep, frontflipAxis) * body.rotation);
            frontflipDegreesRemaining -= rotationStep;
            if (frontflipDegreesRemaining <= 0.01f)
            {
                frontflipActive = false;
                body.angularDamping = 4f;
            }
        }

        private void ApplySuspensionAndAlignment(RaycastHit hit, Vector3 roadTangent)
        {
            float suspensionCompression = Mathf.Clamp01((1.05f - hit.distance) / 0.65f);
            body.AddForce(
                hit.normal * (suspensionCompression * 24f - Vector3.Dot(body.linearVelocity, hit.normal) * 4.5f),
                ForceMode.Acceleration);

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
            float speedRatio = Mathf.Clamp01(speed / normalMaxSpeed);
            float movement = Mathf.InverseLerp(0.2f, 3f, Mathf.Abs(localVelocity.z));
            float direction = Mathf.Abs(localVelocity.z) > 0.2f ? Mathf.Sign(localVelocity.z) : 1f;
            float turnRate = Mathf.Lerp(lowSpeedTurnRate, highSpeedTurnRate, speedRatio);
            float yaw = steering * direction * turnRate * movement * Time.fixedDeltaTime;
            Quaternion steeringRotation = Quaternion.AngleAxis(yaw, transform.up);
            body.MoveRotation(steeringRotation * body.rotation);
        }

        private void ApplyDrive(float throttle, bool useNitro)
        {
            Vector3 localVelocity = transform.InverseTransformDirection(body.linearVelocity);
            float forwardSpeed = Mathf.Abs(localVelocity.z);
            float roadGrip = onRoad ? 1f : 0.72f;

            float speedLimit = useNitro ? nitroMaxSpeed : normalMaxSpeed;
            if (forwardSpeed < speedLimit || Mathf.Sign(throttle) != Mathf.Sign(localVelocity.z))
            {
                float acceleration = throttle >= 0f
                    ? useNitro ? nitroAcceleration : forwardAcceleration
                    : reverseAcceleration;
                if (throttle > 0.05f && onRoad)
                {
                    float uphillAmount = Mathf.Max(0f, transform.forward.y);
                    acceleration += uphillAmount * (Physics.gravity.magnitude * 1.35f + 5f);
                }

                body.AddForce(transform.forward * (throttle * acceleration * roadGrip), ForceMode.Acceleration);
            }

            Vector3 lateralVelocity = transform.right * localVelocity.x;
            body.AddForce(-lateralVelocity * (onRoad ? 7.5f : 3.5f), ForceMode.Acceleration);

            if (!onRoad)
            {
                body.AddForce(-body.linearVelocity * 0.22f, ForceMode.Acceleration);
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

        public void ResetToSafePoint()
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            if (road.TryGetClosestRoadPose(transform.position, out Vector3 roadPoint, out Vector3 roadTangent))
            {
                Vector3 recoveryPosition = roadPoint + Vector3.up * 1.35f;
                Quaternion recoveryRotation = Quaternion.LookRotation(
                    Vector3.ProjectOnPlane(roadTangent, Vector3.up).normalized,
                    Vector3.up);
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
