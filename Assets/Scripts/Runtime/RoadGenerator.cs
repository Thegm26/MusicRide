using System.Collections.Generic;
using UnityEngine;

namespace MusicRoad
{
    public sealed class RoadGenerator : MonoBehaviour
    {
        public const float RoadHalfWidth = 6.5f;
        private const float ShoulderHalfWidth = 14f;
        private const float ChunkLength = 20f;
        private const float SampleSpacing = 2f;
        private const int TargetChunkCount = 16;

        private readonly List<RoadChunk> chunks = new List<RoadChunk>();
        private MusicWorldController music;
        private Transform car;
        private Material roadMaterial;
        private Material shoulderMaterial;
        private Material edgeMaterial;

        private Vector3 cursor = Vector3.zero;
        private float slope;
        private int sequence;
        private readonly float seed = 18.247f;

        public void Initialize(
            MusicWorldController musicController,
            Transform carTransform,
            Material road,
            Material shoulder,
            Material edge)
        {
            music = musicController;
            car = carTransform;
            roadMaterial = road;
            shoulderMaterial = shoulder;
            edgeMaterial = edge;

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

            return new RoadChunk
            {
                root = root,
                mesh = mesh,
                collider = collider,
                samples = new List<Vector3>()
            };
        }

        private void PopulateChunk(RoadChunk chunk)
        {
            chunk.root.name = $"Road Chunk {sequence:0000}";
            chunk.samples.Clear();

            AudioFeatureFrame features = music != null ? music.Delayed : default;
            float energy = Mathf.Clamp01(features.intensity * 1.25f + features.vocal * 0.85f);
            float amount = sequence < 2 ? 0f : Mathf.Lerp(0.55f, 1f, energy);
            float hillNoise = Mathf.PerlinNoise(sequence * 0.09f, seed + 4.2f) * 2f - 1f;
            float hillWave = Mathf.Sin(sequence * 0.92f + seed) * 0.9f + hillNoise * 0.72f;
            float targetSlope = hillWave * Mathf.Lerp(0.2f, 0.58f, Mathf.Clamp01(features.intensity * 0.7f + features.vocal * 0.75f)) * amount;
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

                slope = Mathf.MoveTowards(slope, targetSlope, 0.024f);
                cursor += Vector3.forward * SampleSpacing + Vector3.up * (slope * SampleSpacing);
            }

            BuildMesh(chunk);
            chunk.boundsCenter = chunk.samples[chunk.samples.Count / 2];
            sequence++;
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
                    -ShoulderHalfWidth,
                    -RoadHalfWidth,
                    -RoadHalfWidth + 0.22f,
                    -0.11f,
                    0.11f,
                    RoadHalfWidth - 0.22f,
                    RoadHalfWidth,
                    ShoulderHalfWidth
                };
                for (int column = 0; column < verticesPerRow; column++)
                {
                    int index = i * verticesPerRow + column;
                    vertices[index] = chunk.samples[i] + right * offsets[column];
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
            public Vector3 boundsCenter;
        }
    }
}
