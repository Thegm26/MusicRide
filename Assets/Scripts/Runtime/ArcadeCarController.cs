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
        private Vector3 currentRoadUp = Vector3.up;
        private float jumpMagnetRelease;
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
            body.useGravity = false;
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
            jumpMagnetRelease = Mathf.Max(0f, jumpMagnetRelease - Time.fixedDeltaTime);
            boosting = nitro && throttle > 0.1f;
            if (boosting && !wasBoosting)
            {
                body.AddForce(transform.forward * 4.5f, ForceMode.VelocityChange);
            }
            UpdateNitroFlames();

            bool nearGeneratedRoad = road.TryGetRoadInfo(
                transform.position,
                out Vector3 roadPoint,
                out Vector3 roadTangent,
                out Vector3 roadUp,
                out Vector3 roadRight,
                out float lateralDistance,
                out float normalDistance);
            currentRoadUp = nearGeneratedRoad ? roadUp : transform.up;
            onRoad = nearGeneratedRoad && lateralDistance <= RoadGenerator.RoadHalfWidth + 0.35f;
            grounded = false;
            if (nearGeneratedRoad)
            {
                if (jumpMagnetRelease <= 0f)
                {
                    grounded = onRoad && Physics.Raycast(
                        transform.position + roadUp * 0.3f,
                        -roadUp,
                        out _,
                        1.8f);
                    float signedLateral = Vector3.Dot(transform.position - roadPoint, roadRight);
                    float stuntStrength = Mathf.Clamp01(Vector3.Angle(roadUp, Vector3.up) / 35f);
                    ApplyMagneticAdhesion(
                        roadPoint,
                        roadTangent,
                        roadUp,
                        roadRight,
                        signedLateral,
                        normalDistance,
                        stuntStrength);
                }
                else
                {
                    body.AddForce(-roadUp * 5f, ForceMode.Acceleration);
                }
            }
            else
            {
                body.AddForce(Physics.gravity, ForceMode.Acceleration);
            }

            ApplySteering(steering);

            if (grounded || nearGeneratedRoad)
            {
                ApplyDrive(throttle, boosting);
                if (grounded && jumpRequested)
                {
                    jumpMagnetRelease = 0.65f;
                    body.AddForce(roadUp * 14f + transform.forward * 1.2f, ForceMode.VelocityChange);
                }
            }
            else
            {
                body.AddForce(transform.forward * (throttle * (boosting ? 14f : 3.5f)), ForceMode.Acceleration);
            }

            jumpRequested = false;
            wasBoosting = boosting;
        }

        private void ApplyMagneticAdhesion(
            Vector3 roadPoint,
            Vector3 roadTangent,
            Vector3 roadUp,
            Vector3 roadRight,
            float signedLateral,
            float normalDistance,
            float stuntStrength)
        {
            const float rideHeight = 0.78f;
            float clampedLateral = signedLateral;
            if (stuntStrength > 0.05f)
            {
                float stuntHalfWidth = Mathf.Lerp(
                    RoadGenerator.RoadHalfWidth - 0.65f,
                    RoadGenerator.RoadHalfWidth - 2f,
                    stuntStrength);
                clampedLateral = Mathf.Clamp(signedLateral, -stuntHalfWidth, stuntHalfWidth);
            }

            Vector3 constrainedPosition =
                roadPoint +
                roadRight * clampedLateral +
                roadUp * rideHeight;
            float positionGrip = 1f - Mathf.Exp(
                -Time.fixedDeltaTime * Mathf.Lerp(10f, 32f, stuntStrength));
            body.position = Vector3.Lerp(body.position, constrainedPosition, positionGrip);

            float heightError = normalDistance - rideHeight;
            float normalSpeed = Vector3.Dot(body.linearVelocity, roadUp);
            float magneticForce = -heightError * 88f - normalSpeed * 13f - 18f;
            body.AddForce(roadUp * magneticForce, ForceMode.Acceleration);

            Vector3 desiredForward = Vector3.ProjectOnPlane(transform.forward, roadUp).normalized;
            if (desiredForward.sqrMagnitude < 0.1f)
            {
                desiredForward = roadTangent;
            }

            Vector3 trackDirection = Vector3.Dot(desiredForward, roadTangent) >= 0f
                ? roadTangent
                : -roadTangent;
            desiredForward = Vector3.Slerp(
                desiredForward,
                trackDirection,
                Mathf.Lerp(onRoad ? 0.22f : 0.1f, 0.76f, stuntStrength)).normalized;
            Quaternion desiredRotation = Quaternion.LookRotation(desiredForward, roadUp);
            body.MoveRotation(Quaternion.Slerp(
                body.rotation,
                desiredRotation,
                Time.fixedDeltaTime * Mathf.Lerp(11f, 28f, stuntStrength)));

            float forwardSpeed = Vector3.Dot(body.linearVelocity, trackDirection);
            float lateralSpeed = Vector3.Dot(body.linearVelocity, roadRight);
            Vector3 constrainedVelocity =
                trackDirection * forwardSpeed +
                roadRight * lateralSpeed * Mathf.Lerp(1f, 0.35f, stuntStrength);
            float velocityGrip = 1f - Mathf.Exp(
                -Time.fixedDeltaTime * Mathf.Lerp(5f, 30f, stuntStrength));
            body.linearVelocity = Vector3.Lerp(body.linearVelocity, constrainedVelocity, velocityGrip);
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
            Quaternion steeringRotation = Quaternion.AngleAxis(yaw, currentRoadUp);
            body.MoveRotation(steeringRotation * body.rotation);
        }

        private void ApplyDrive(float throttle, bool useNitro)
        {
            Vector3 localVelocity = transform.InverseTransformDirection(body.linearVelocity);
            float speed = body.linearVelocity.magnitude;
            float roadGrip = onRoad ? 1f : 0.72f;

            float speedLimit = useNitro ? NitroMaxSpeed : NormalMaxSpeed;
            if (speed < speedLimit || Mathf.Sign(throttle) != Mathf.Sign(localVelocity.z))
            {
                float acceleration = throttle >= 0f
                    ? useNitro ? 58f : 18f
                    : 11f;
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
            if (road.TryGetRoadInfo(
                transform.position,
                out Vector3 roadPoint,
                out Vector3 roadTangent,
                out Vector3 roadUp,
                out _,
                out _,
                out _))
            {
                Vector3 recoveryPosition = roadPoint + roadUp * 1.35f;
                Quaternion recoveryRotation = Quaternion.LookRotation(roadTangent, roadUp);
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
