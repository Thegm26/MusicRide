using UnityEngine;

namespace MusicRoad
{
    public sealed class ChaseCamera : MonoBehaviour
    {
        private Transform target;
        private MusicWorldController music;
        private Vector3 velocity;

        public void Initialize(Transform followTarget, MusicWorldController musicController)
        {
            target = followTarget;
            music = musicController;
            transform.position = target.position - target.forward * 8.5f + Vector3.up * 5.2f;
            transform.LookAt(target.position + target.forward * 4f);
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            Vector3 desired = target.position - target.forward * 8.5f + Vector3.up * 5.2f;
            transform.position = Vector3.SmoothDamp(transform.position, desired, ref velocity, 0.18f);
            Vector3 focus = target.position + target.forward * 5f + Vector3.up * 0.8f;
            Quaternion look = Quaternion.LookRotation(focus - transform.position, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, look, Time.deltaTime * 7f);

            if (music != null)
            {
                Camera camera = GetComponent<Camera>();
                if (camera != null)
                {
                    camera.fieldOfView = Mathf.Lerp(camera.fieldOfView, 66f + music.Immediate.rms * 8f + music.BeatPulse * 5f, Time.deltaTime * 9f);
                }

                if (music.BeatPulse > 0.01f)
                {
                    transform.position += transform.right * Mathf.Sin(Time.time * 42f) * music.BeatPulse * 0.11f;
                }
            }
        }
    }
}
