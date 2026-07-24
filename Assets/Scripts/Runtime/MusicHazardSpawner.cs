using System.Collections.Generic;
using UnityEngine;

namespace MusicRoad
{
    public sealed class MusicHazardSpawner : MonoBehaviour
    {
        private const int PoolSize = 12;
        private readonly List<MusicHazard> pool = new List<MusicHazard>();

        private MusicWorldController music;
        private Transform car;
        private RoadGenerator road;
        private Material material;
        private float spawnTimer;
        private float previousBeatPulse;
        private int shapeIndex;

        public void Initialize(
            MusicWorldController controller,
            Transform carTransform,
            RoadGenerator roadGenerator,
            Material hazardMaterial)
        {
            music = controller;
            car = carTransform;
            road = roadGenerator;
            material = hazardMaterial;

            for (int i = 0; i < PoolSize; i++)
            {
                pool.Add(CreateHazard(i));
            }
        }

        private void Update()
        {
            if (music == null || car == null || road == null)
            {
                return;
            }

            spawnTimer -= Time.deltaTime;
            AudioFeatureFrame frame = music.Immediate;
            bool beatStarted = music.BeatPulse > 0.62f && previousBeatPulse <= 0.62f;

            if (beatStarted && spawnTimer <= 0f)
            {
                int burst = frame.treble > 0.72f ? 2 : 1;
                for (int i = 0; i < burst; i++)
                {
                    PlaceHazard(i, burst);
                }

                spawnTimer = Mathf.Lerp(0.9f, 0.48f, frame.rms);
            }
            else if (frame.rms > 0.2f && spawnTimer <= 0f)
            {
                PlaceHazard(0, 1);
                spawnTimer = Mathf.Lerp(2.2f, 1f, frame.rms);
            }

            previousBeatPulse = music.BeatPulse;
        }

        private MusicHazard CreateHazard(int index)
        {
            GameObject hazardObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            hazardObject.name = $"Beat Hazard {index + 1:00}";
            hazardObject.transform.SetParent(transform, false);
            hazardObject.GetComponent<Renderer>().sharedMaterial = material;

            Rigidbody body = hazardObject.AddComponent<Rigidbody>();
            body.mass = 5f;
            body.useGravity = false;
            body.isKinematic = true;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.interpolation = RigidbodyInterpolation.Interpolate;

            MusicHazard hazard = hazardObject.AddComponent<MusicHazard>();
            hazard.Initialize(body);
            hazardObject.SetActive(false);
            return hazard;
        }

        private void PlaceHazard(int burstIndex, int burstCount)
        {
            MusicHazard hazard = GetAvailableHazard();
            if (hazard == null)
            {
                return;
            }

            float distance = Random.Range(25f, 42f);
            Vector3 probe = car.position + car.forward * distance;
            if (!road.TryGetRoadInfo(probe, out Vector3 roadPoint, out Vector3 tangent, out _))
            {
                return;
            }

            Vector3 right = Vector3.Cross(Vector3.up, tangent).normalized;
            float lane = burstCount <= 1
                ? Random.Range(-4.7f, 4.7f)
                : Mathf.Lerp(-4.4f, 4.4f, burstIndex / (burstCount - 1f));
            const float scale = 2f;
            Vector3 roadUp = Vector3.Cross(tangent, right).normalized;
            Vector3 position = roadPoint + right * lane + roadUp * (scale * 0.5f);
            Quaternion rotation = Quaternion.LookRotation(tangent, roadUp);

            hazard.Place(position, rotation, scale);
        }

        private MusicHazard GetAvailableHazard()
        {
            for (int i = 0; i < pool.Count; i++)
            {
                shapeIndex = (shapeIndex + 1) % pool.Count;
                if (!pool[shapeIndex].gameObject.activeSelf)
                {
                    return pool[shapeIndex];
                }
            }

            return null;
        }
    }

    public sealed class MusicHazard : MonoBehaviour
    {
        private Rigidbody body;
        private float remainingLife;

        public void Initialize(Rigidbody hazardBody)
        {
            body = hazardBody;
        }

        public void Place(Vector3 position, Quaternion rotation, float scale)
        {
            gameObject.SetActive(true);
            transform.position = position;
            transform.localScale = Vector3.one * scale;
            transform.rotation = rotation;
            remainingLife = 10f;

            body.position = position;
            body.rotation = rotation;
        }

        private void Update()
        {
            remainingLife -= Time.deltaTime;
            if (remainingLife <= 0f)
            {
                Deactivate();
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (collision.collider.GetComponentInParent<ArcadeCarController>() != null)
            {
                RunManager.Instance?.BreakCombo();
                Deactivate();
            }
        }

        private void Deactivate()
        {
            if (body != null)
            {
                body.position = transform.position;
            }

            gameObject.SetActive(false);
        }
    }
}
