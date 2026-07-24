using System.Collections.Generic;
using UnityEngine;

namespace MusicRoad
{
    public sealed class RoadGenerator : MonoBehaviour
    {
        public const float RoadHalfWidth = 6.5f;
        private const float LandscapeHalfWidth = 44f;
        private const float LandscapeEdgeDrop = 2.2f;
        private const float ChunkLength = 20f;
        private const float SampleSpacing = 2f;
        private const int TargetChunkCount = 9;
        private static readonly int[] EnvironmentPattern =
        {
            0, 1, 0, 1, 3, 0, 1, 4,
            5, 0, 1, 6, 7, 0, 8, 9
        };

        private readonly List<RoadChunk> chunks = new List<RoadChunk>();
        private MusicWorldController music;
        private Transform car;
        private Material roadMaterial;
        private Material shoulderMaterial;
        private Material edgeMaterial;
        private Material trunkMaterial;
        private Material foliageMaterial;
        private Material rockMaterial;
        private GameObject[] environmentPrefabs;

        private Vector3 cursor = Vector3.zero;
        private float yaw;
        private float yawRate;
        private float slope;
        private int sequence;
        private readonly float seed = 18.247f;

        public void Initialize(
            MusicWorldController musicController,
            Transform carTransform,
            Material road,
            Material shoulder,
            Material edge,
            Material trunk,
            Material foliage,
            Material rock,
            GameObject[] importedEnvironment)
        {
            music = musicController;
            car = carTransform;
            roadMaterial = road;
            shoulderMaterial = shoulder;
            edgeMaterial = edge;
            trunkMaterial = trunk;
            foliageMaterial = foliage;
            rockMaterial = rock;
            environmentPrefabs = importedEnvironment;

            for (int i = 0; i < TargetChunkCount; i++)
            {
                RoadChunk chunk = CreateChunk(i);
                chunks.Add(chunk);
                PopulateChunk(chunk);
            }
        }

        private void Update()
        {
            if (car == null || chunks.Count == 0)
            {
                return;
            }

            int nearestChunk = FindNearestChunkIndex(car.position);
            while (nearestChunk > 3)
            {
                RoadChunk recycled = chunks[0];
                chunks.RemoveAt(0);
                PopulateChunk(recycled);
                chunks.Add(recycled);
                nearestChunk--;
            }
        }

        public bool TryGetRoadInfo(Vector3 worldPosition, out Vector3 point, out Vector3 tangent, out float lateralDistance)
        {
            bool found = TryGetClosestRoadPose(worldPosition, out point, out tangent, out float bestSqr);
            if (!found)
            {
                lateralDistance = float.MaxValue;
                return false;
            }

            Vector3 planarOffset = Vector3.ProjectOnPlane(worldPosition - point, Vector3.up);
            lateralDistance = planarOffset.magnitude;
            return bestSqr < 55f * 55f;
        }

        public bool TryGetClosestRoadPose(Vector3 worldPosition, out Vector3 point, out Vector3 tangent)
        {
            return TryGetClosestRoadPose(worldPosition, out point, out tangent, out _);
        }

        private bool TryGetClosestRoadPose(Vector3 worldPosition, out Vector3 point, out Vector3 tangent, out float bestSqr)
        {
            point = Vector3.zero;
            tangent = Vector3.forward;
            bestSqr = float.MaxValue;
            bool found = false;

            foreach (RoadChunk chunk in chunks)
            {
                for (int i = 0; i < chunk.samples.Count; i++)
                {
                    float sqr = (chunk.samples[i] - worldPosition).sqrMagnitude;
                    if (sqr >= bestSqr)
                    {
                        continue;
                    }

                    bestSqr = sqr;
                    found = true;
                    point = chunk.samples[i];
                    if (i < chunk.samples.Count - 1)
                    {
                        tangent = (chunk.samples[i + 1] - chunk.samples[i]).normalized;
                    }
                    else if (i > 0)
                    {
                        tangent = (chunk.samples[i] - chunk.samples[i - 1]).normalized;
                    }
                }
            }

            return found;
        }

        public Vector3 GetStartPosition()
        {
            return chunks.Count > 0 && chunks[0].samples.Count > 2
                ? chunks[0].samples[2] + Vector3.up * 0.9f
                : new Vector3(0f, 0.9f, 4f);
        }

        public Quaternion GetStartRotation()
        {
            if (chunks.Count == 0 || chunks[0].samples.Count < 4)
            {
                return Quaternion.identity;
            }

            Vector3 forward = chunks[0].samples[3] - chunks[0].samples[2];
            return Quaternion.LookRotation(Vector3.ProjectOnPlane(forward, Vector3.up).normalized, Vector3.up);
        }

        private int FindNearestChunkIndex(Vector3 position)
        {
            int bestIndex = 0;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < chunks.Count; i++)
            {
                float sqr = (chunks[i].boundsCenter - position).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        private RoadChunk CreateChunk(int index)
        {
            GameObject root = new GameObject($"Road Chunk {index:00}");
            root.transform.SetParent(transform, false);

            MeshFilter filter = root.AddComponent<MeshFilter>();
            MeshRenderer renderer = root.AddComponent<MeshRenderer>();
            MeshCollider collider = root.AddComponent<MeshCollider>();
            renderer.sharedMaterials = new[] { shoulderMaterial, roadMaterial, edgeMaterial };

            Mesh mesh = new Mesh { name = $"Runtime Road Chunk {index:00}" };
            mesh.MarkDynamic();
            filter.sharedMesh = mesh;

            RoadChunk chunk = new RoadChunk
            {
                root = root,
                mesh = mesh,
                collider = collider,
                samples = new List<Vector3>(),
                environmentSlots = new List<Transform>()
            };

            if (environmentPrefabs != null && environmentPrefabs.Length > 0)
            {
                for (int i = 0; i < EnvironmentPattern.Length; i++)
                {
                    int patternIndex = (index * 3 + i) % EnvironmentPattern.Length;
                    int prefabIndex = EnvironmentPattern[patternIndex] % environmentPrefabs.Length;
                    GameObject prefab = environmentPrefabs[prefabIndex];
                    if (prefab == null)
                    {
                        continue;
                    }

                    GameObject instance = Instantiate(prefab, root.transform);
                    instance.name = $"ENV_SLOT_{i + 1:00}__{prefab.name}";
                    RemoveEnvironmentPhysics(instance);
                    chunk.environmentSlots.Add(instance.transform);
                }
            }
            else
            {
                for (int i = 0; i < 6; i++)
                {
                    chunk.environmentSlots.Add(CreateTreeSlot(root.transform, i));
                }
                for (int i = 0; i < 3; i++)
                {
                    chunk.environmentSlots.Add(CreateRockSlot(root.transform, i));
                }
            }
            return chunk;
        }

        private void PopulateChunk(RoadChunk chunk)
        {
            chunk.root.name = $"Road Chunk {sequence:0000}";
            chunk.samples.Clear();

            AudioFeatureFrame features = music != null ? music.Delayed : default;
            float section = Mathf.Clamp01(features.sectionLift);
            float turnDrive = Mathf.Clamp01(features.harmonicChange * 0.62f + features.beatDensity * 0.38f);
            float hillDrive = Mathf.Clamp01(features.sectionLift * 0.58f + features.fullness * 0.24f + features.lowImpact * 0.18f);
            float amount = sequence < 2 ? 0f : Mathf.Lerp(0.52f, 1f, section);
            float turnNoise = Mathf.PerlinNoise(seed, sequence * 0.115f) * 2f - 1f;
            float hillNoise = Mathf.PerlinNoise(sequence * 0.09f, seed + 4.2f) * 2f - 1f;
            float hillWave = Mathf.Sin(sequence * 0.92f + seed) * 0.9f + hillNoise * 0.72f;
            float targetYawRate = turnNoise * Mathf.Lerp(0.62f, 2.05f, turnDrive) * amount;
            float targetSlope = hillWave * Mathf.Lerp(0.2f, 0.64f, hillDrive) * amount;
            targetSlope -= Mathf.Clamp(cursor.y * 0.009f, -0.13f, 0.13f);
            targetSlope = Mathf.Clamp(targetSlope, -0.48f, 0.48f);

            int sampleCount = Mathf.RoundToInt(ChunkLength / SampleSpacing);
            for (int i = 0; i <= sampleCount; i++)
            {
                chunk.samples.Add(cursor);
                if (i == sampleCount)
                {
                    break;
                }

                yawRate = Mathf.MoveTowards(yawRate, targetYawRate, 0.12f);
                slope = Mathf.MoveTowards(slope, targetSlope, 0.024f);
                yaw += yawRate * SampleSpacing;
                Vector3 forward = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
                cursor += forward * SampleSpacing + Vector3.up * (slope * SampleSpacing);
            }

            BuildMesh(chunk);
            PopulateEnvironment(chunk);
            chunk.boundsCenter = chunk.samples[chunk.samples.Count / 2];
            sequence++;
        }

        private void PopulateEnvironment(RoadChunk chunk)
        {
            for (int i = 0; i < chunk.environmentSlots.Count; i++)
            {
                int sampleIndex = Mathf.Clamp(
                    Mathf.RoundToInt(Mathf.Lerp(1f, chunk.samples.Count - 2f, (i + 0.5f) / chunk.environmentSlots.Count)),
                    1,
                    chunk.samples.Count - 2);
                Vector3 tangent = (chunk.samples[sampleIndex + 1] - chunk.samples[sampleIndex]).normalized;
                Vector3 right = Vector3.Cross(Vector3.up, tangent).normalized;
                float side = i % 2 == 0 ? -1f : 1f;
                Transform item = chunk.environmentSlots[i];
                string itemName = item.name.ToLowerInvariant();
                bool smallFoliage = itemName.Contains("grass") || itemName.Contains("mushroom");
                bool sign = itemName.Contains("sign");
                bool stone = itemName.Contains("stone") || itemName.Contains("rock");
                bool tree = itemName.Contains("tree") && !itemName.Contains("stump");
                int forestLayer = (i / 2) % 3;
                float distance = sign
                    ? Random.Range(8.2f, 9.5f)
                    : smallFoliage
                        ? Random.Range(8.5f, 15f)
                        : tree
                            ? 14f + forestLayer * 10f + Random.Range(0f, 5f)
                            : Random.Range(10f, 22f);
                float landscapeAmount = Mathf.InverseLerp(RoadHalfWidth, LandscapeHalfWidth, distance);
                float groundOffset = -LandscapeEdgeDrop * landscapeAmount;
                item.position = chunk.samples[sampleIndex] + right * side * distance + Vector3.up * groundOffset;
                item.rotation = Quaternion.Euler(
                    stone ? Random.Range(-8f, 8f) : 0f,
                    Random.Range(0f, 360f),
                    stone ? Random.Range(-8f, 8f) : 0f);
                float scale = smallFoliage
                    ? Random.Range(1.45f, 2.35f)
                    : tree
                        ? Random.Range(0.95f, 1.42f)
                        : Random.Range(0.85f, 1.35f);
                item.localScale = Vector3.one * scale;
            }
        }

        private static void RemoveEnvironmentPhysics(GameObject environment)
        {
            Collider[] colliders = environment.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Destroy(colliders[i]);
            }

            Rigidbody[] bodies = environment.GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < bodies.Length; i++)
            {
                Destroy(bodies[i]);
            }
        }

        private Transform CreateTreeSlot(Transform parent, int index)
        {
            GameObject root = new GameObject($"TREE_SLOT_{index + 1:00}");
            root.transform.SetParent(parent, false);
            CreateEnvironmentPrimitive(
                root.transform,
                "Placeholder Trunk",
                PrimitiveType.Cylinder,
                new Vector3(0f, 1.45f, 0f),
                new Vector3(0.5f, 1.45f, 0.5f),
                trunkMaterial);
            CreateEnvironmentPrimitive(
                root.transform,
                "Placeholder Foliage",
                PrimitiveType.Sphere,
                new Vector3(0f, 4.2f, 0f),
                new Vector3(3.2f, 4f, 3.2f),
                foliageMaterial);
            return root.transform;
        }

        private Transform CreateRockSlot(Transform parent, int index)
        {
            GameObject root = new GameObject($"ROCK_SLOT_{index + 1:00}");
            root.transform.SetParent(parent, false);
            CreateEnvironmentPrimitive(
                root.transform,
                "Placeholder Rock",
                PrimitiveType.Cube,
                new Vector3(0f, 0.65f, 0f),
                new Vector3(2.4f, 1.3f, 1.8f),
                rockMaterial);
            return root.transform;
        }

        private static void CreateEnvironmentPrimitive(
            Transform parent,
            string name,
            PrimitiveType type,
            Vector3 localPosition,
            Vector3 localScale,
            Material material)
        {
            GameObject primitive = GameObject.CreatePrimitive(type);
            primitive.name = name;
            primitive.transform.SetParent(parent, false);
            primitive.transform.localPosition = localPosition;
            primitive.transform.localScale = localScale;
            primitive.GetComponent<Renderer>().sharedMaterial = material;
            Collider collider = primitive.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }
        }

        private void BuildMesh(RoadChunk chunk)
        {
            const int verticesPerRow = 8;
            int rowCount = chunk.samples.Count;
            var vertices = new Vector3[rowCount * verticesPerRow];
            var uvs = new Vector2[vertices.Length];
            var shoulderTriangles = new List<int>();
            var roadTriangles = new List<int>();
            var edgeTriangles = new List<int>();

            for (int i = 0; i < rowCount; i++)
            {
                Vector3 tangent;
                if (i == rowCount - 1)
                {
                    tangent = chunk.samples[i] - chunk.samples[i - 1];
                }
                else
                {
                    tangent = chunk.samples[i + 1] - chunk.samples[i];
                }

                Vector3 right = Vector3.Cross(Vector3.up, tangent.normalized).normalized;
                float[] offsets =
                {
                    -LandscapeHalfWidth,
                    -RoadHalfWidth,
                    -RoadHalfWidth + 0.22f,
                    -0.11f,
                    0.11f,
                    RoadHalfWidth - 0.22f,
                    RoadHalfWidth,
                    LandscapeHalfWidth
                };
                for (int column = 0; column < verticesPerRow; column++)
                {
                    int index = i * verticesPerRow + column;
                    float edgeDrop = column == 0 || column == verticesPerRow - 1
                        ? LandscapeEdgeDrop
                        : 0f;
                    vertices[index] = chunk.samples[i] + right * offsets[column] - Vector3.up * edgeDrop;
                    uvs[index] = new Vector2(column / (verticesPerRow - 1f), (sequence * ChunkLength + i * SampleSpacing) * 0.08f);
                }
            }

            for (int row = 0; row < rowCount - 1; row++)
            {
                AddStrip(shoulderTriangles, row, 0, verticesPerRow);
                AddStrip(edgeTriangles, row, 1, verticesPerRow);
                AddStrip(roadTriangles, row, 2, verticesPerRow);
                AddStrip(edgeTriangles, row, 3, verticesPerRow);
                AddStrip(roadTriangles, row, 4, verticesPerRow);
                AddStrip(edgeTriangles, row, 5, verticesPerRow);
                AddStrip(shoulderTriangles, row, 6, verticesPerRow);
            }

            chunk.mesh.Clear();
            chunk.mesh.vertices = vertices;
            chunk.mesh.uv = uvs;
            chunk.mesh.subMeshCount = 3;
            chunk.mesh.SetTriangles(shoulderTriangles, 0);
            chunk.mesh.SetTriangles(roadTriangles, 1);
            chunk.mesh.SetTriangles(edgeTriangles, 2);
            chunk.mesh.RecalculateNormals();
            chunk.mesh.RecalculateBounds();

            chunk.collider.sharedMesh = null;
            chunk.collider.sharedMesh = chunk.mesh;
        }

        private static void AddStrip(List<int> triangles, int row, int column, int rowWidth)
        {
            int a = row * rowWidth + column;
            int b = a + 1;
            int c = a + rowWidth;
            int d = c + 1;
            triangles.Add(a);
            triangles.Add(c);
            triangles.Add(b);
            triangles.Add(b);
            triangles.Add(c);
            triangles.Add(d);
        }

        private sealed class RoadChunk
        {
            public GameObject root;
            public Mesh mesh;
            public MeshCollider collider;
            public List<Vector3> samples;
            public List<Transform> environmentSlots;
            public Vector3 boundsCenter;
        }
    }
}
