using UnityEngine;

namespace MusicRoad
{
    public sealed class ChaseCamera : MonoBehaviour
    {
        private Transform target;
        private MusicWorldController music;
        private ArcadeCarController car;
        private Rigidbody targetBody;
        private Vector3 velocity;
        private Vector3 travelForward;
        private float groundHeight;
        private float groundHeightVelocity;

        public void Initialize(Transform followTarget, MusicWorldController musicController)
        {
            target = followTarget;
            music = musicController;
            car = target.GetComponent<ArcadeCarController>();
            targetBody = target.GetComponent<Rigidbody>();
            travelForward = Vector3.ProjectOnPlane(target.forward, Vector3.up).normalized;
            groundHeight = target.position.y;
            Vector3 anchor = new Vector3(target.position.x, groundHeight, target.position.z);
            transform.position = anchor - travelForward * 8.5f + Vector3.up * 5.2f;
            transform.LookAt(anchor + travelForward * 4f);
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            bool frontflipping = car != null && car.IsFrontflipping;
            if (frontflipping && targetBody != null)
            {
                Vector3 movement = Vector3.ProjectOnPlane(targetBody.linearVelocity, Vector3.up);
                if (movement.sqrMagnitude > 1f)
                {
                    travelForward = movement.normalized;
                }
            }
            else
            {
                Vector3 heading = Vector3.ProjectOnPlane(target.forward, Vector3.up);
                if (heading.sqrMagnitude > 0.1f)
                {
                    travelForward = Vector3.Slerp(
                        travelForward,
                        heading.normalized,
                        Time.deltaTime * 8f);
                }
            }

            if (car == null || (car.IsGrounded && !frontflipping))
            {
                groundHeight = Mathf.SmoothDamp(
                    groundHeight,
                    target.position.y,
                    ref groundHeightVelocity,
                    0.14f);
            }

            Vector3 anchor = new Vector3(target.position.x, groundHeight, target.position.z);
            Vector3 desired = anchor - travelForward * 8.5f + Vector3.up * 5.2f;
            transform.position = Vector3.SmoothDamp(transform.position, desired, ref velocity, 0.18f);
            Vector3 focus = anchor + travelForward * 5f + Vector3.up * 0.8f;
            Quaternion look = Quaternion.LookRotation(focus - transform.position, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, look, Time.deltaTime * 7f);

            if (music != null)
            {
                Camera camera = GetComponent<Camera>();
                if (camera != null)
                {
                    float boostFov = car != null && car.IsBoosting ? 8f : 0f;
                    camera.fieldOfView = Mathf.Lerp(
                        camera.fieldOfView,
                        60f + music.Immediate.rms * 3f + boostFov,
                        Time.deltaTime * 8f);
                }
            }
        }
    }
}
