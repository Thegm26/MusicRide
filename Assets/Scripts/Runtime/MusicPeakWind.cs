using UnityEngine;

namespace MusicRoad
{
    public sealed class MusicPeakWind : MonoBehaviour
    {
        private const int PoolSize = 56;
        private readonly Transform[] streaks = new Transform[PoolSize];

        private MusicWorldController music;
        private Transform car;
        private Material material;
        private float strength;

        public void Initialize(MusicWorldController musicController, Transform carTransform, Material streakMaterial)
        {
            music = musicController;
            car = carTransform;
            material = streakMaterial;

            for (int i = 0; i < PoolSize; i++)
            {
                streaks[i] = CreateStreak(i);
                streaks[i].gameObject.SetActive(false);
            }
        }

        private void Update()
        {
            if (music == null || car == null)
            {
                return;
            }

            AudioFeatureFrame frame = music.Immediate;
            float sustainedStrength = Mathf.InverseLerp(0.68f, 0.92f, frame.heavy);
            float hitStrength = frame.onset > 0.82f
                ? Mathf.Clamp01(music.BeatPulse * 1.15f)
                : 0f;
            float targetStrength = Mathf.Max(sustainedStrength, hitStrength);
            if (targetStrength > strength)
            {
                strength = targetStrength;
            }
            else
            {
                strength = Mathf.MoveTowards(strength, targetStrength, Time.deltaTime * 0.95f);
            }

            int activeCount = Mathf.RoundToInt(strength * PoolSize);
            Vector3 windDirection = (-car.forward + Vector3.down * 0.08f).normalized;
            Quaternion windRotation = Quaternion.LookRotation(windDirection, Vector3.up);
            float speed = Mathf.Lerp(34f, 112f, strength);

            for (int i = 0; i < PoolSize; i++)
            {
                Transform streak = streaks[i];
                bool shouldBeActive = i < activeCount;
                if (!shouldBeActive)
                {
                    if (streak.gameObject.activeSelf)
                    {
                        streak.gameObject.SetActive(false);
                    }
                    continue;
                }

                if (!streak.gameObject.activeSelf)
                {
                    streak.gameObject.SetActive(true);
                    ResetStreak(streak, windRotation);
                }

                streak.position += windDirection * (speed * Time.deltaTime);
                streak.rotation = windRotation;
                Vector3 offset = streak.position - car.position;
                if (Vector3.Dot(offset, car.forward) < -10f || offset.sqrMagnitude > 70f * 70f)
                {
                    ResetStreak(streak, windRotation);
                }
            }
        }

        private Transform CreateStreak(int index)
        {
            GameObject streak = GameObject.CreatePrimitive(PrimitiveType.Cube);
            streak.name = $"Peak Wind {index + 1:00}";
            streak.transform.SetParent(transform, false);
            streak.GetComponent<Renderer>().sharedMaterial = material;
            Collider streakCollider = streak.GetComponent<Collider>();
            if (streakCollider != null)
            {
                Destroy(streakCollider);
            }
            return streak.transform;
        }

        private void ResetStreak(Transform streak, Quaternion rotation)
        {
            float width = Random.Range(0.025f, 0.065f);
            float length = Random.Range(0.75f, 2.2f) * Mathf.Lerp(0.7f, 1.35f, strength);
            streak.SetPositionAndRotation(
                car.position +
                car.forward * Random.Range(15f, 55f) +
                car.right * Random.Range(-14f, 14f) +
                Vector3.up * Random.Range(0.3f, 10f),
                rotation);
            streak.localScale = new Vector3(width, width, length);
        }
    }
}
