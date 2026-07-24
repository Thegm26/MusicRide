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
        private Quaternion frame = Quaternion.identity;
        private float yawRate;
        private float pitchRate;
        private float rollRate;
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

        public bool TryGetRoadInfo(
            Vector3 worldPosition,
            out Vector3 point,
            out Vector3 tangent,
            out Vector3 roadUp,
            out Vector3 roadRight,
            out float lateralDistance,
            out float normalDistance)
        {
            point = Vector3.zero;
            tangent = Vector3.forward;
            roadUp = Vector3.up;
            roadRight = Vector3.right;
            lateralDistance = float.MaxValue;
            normalDistance = 0f;
            float bestSqr = float.MaxValue;
            RoadChunk closestChunk = null;
            int closestSample = -1;

            for (int chunkIndex = 0; chunkIndex < chunks.Count; chunkIndex++)
            {
                RoadChunk chunk = chunks[chunkIndex];
                for (int sampleIndex = 0; sampleIndex < chunk.samples.Count; sampleIndex++)
                {
                    float sqr = (chunk.samples[sampleIndex] - worldPosition).sqrMagnitude;
                    if (sqr < bestSqr)
                    {
                        bestSqr = sqr;
                        closestChunk = chunk;
                        closestSample = sampleIndex;
                    }
                }
            }

            if (closestChunk == null)
            {
                return false;
            }

            point = closestChunk.samples[closestSample];
            roadUp = closestChunk.ups[closestSample].normalized;
            if (closestSample < closestChunk.samples.Count - 1)
            {
                tangent = (closestChunk.samples[closestSample + 1] - point).normalized;
            }
            else if (closestSample > 0)
            {
                tangent = (point - closestChunk.samples[closestSample - 1]).normalized;
            }

            roadRight = Vector3.Cross(roadUp, tangent).normalized;
            roadUp = Vector3.Cross(tangent, roadRight).normalized;
            Vector3 offset = worldPosition - point;
            lateralDistance = Mathf.Abs(Vector3.Dot(offset, roadRight));
            normalDistance = Vector3.Dot(offset, roadUp);
            return bestSqr < 60f * 60f;
        }

        public Vector3 GetStartPosition()
        {
            return chunks.Count > 0 && chunks[0].samples.Count > 2
                ? chunks[0].samples[2] + chunks[0].ups[2] * 0.9f
                : new Vector3(0f, 0.9f, 4f);
        }

        public Quaternion GetStartRotation()
        {
            if (chunks.Count == 0 || chunks[0].samples.Count < 4)
            {
                return Quaternion.identity;
            }

            Vector3 forward = chunks[0].samples[3] - chunks[0].samples[2];
            return Quaternion.LookRotation(forward.normalized, chunks[0].ups[2]);
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
                samples = new List<Vector3>(),
                ups = new List<Vector3>()
            };
        }

        private void PopulateChunk(RoadChunk chunk)
        {
            chunk.root.name = $"Road Chunk {sequence:0000}";
            chunk.samples.Clear();
            chunk.ups.Clear();

            AudioFeatureFrame features = music != null ? music.Delayed : default;
            float energy = Mathf.Clamp01(features.intensity * 1.25f + features.vocal * 0.85f);
            float amount = sequence < 2 ? 0f : Mathf.Lerp(0.55f, 1f, energy);
            float turnNoise = Mathf.PerlinNoise(seed, sequence * 0.115f) * 2f - 1f;
            float hillNoise = Mathf.PerlinNoise(sequence * 0.09f, seed + 4.2f) * 2f - 1f;
            float hillWave = Mathf.Sin(sequence * 0.92f + seed) * 0.9f + hillNoise * 0.72f;
            float targetYawRate = turnNoise * Mathf.Lerp(0.55f, 2.6f, Mathf.Clamp01(features.vocal * 1.25f)) * amount;
            float targetPitchRate = hillWave * Mathf.Lerp(0.35f, 1.35f, Mathf.Clamp01(features.intensity * 0.7f + features.vocal * 0.75f)) * amount;
            targetPitchRate += Mathf.Clamp(cursor.y * 0.006f, -0.55f, 0.55f);
            float targetRollRate = 0f;

            int stuntPhase = sequence % 18;
            bool halfLoop = stuntPhase >= 5 && stuntPhase <= 7;
            bool rollExit = stuntPhase >= 8 && stuntPhase <= 9;
            if (halfLoop)
            {
                targetYawRate = 0f;
                targetPitchRate = -3f;
                targetRollRate = 0f;
            }
            else if (rollExit)
            {
                targetYawRate = 0f;
                targetPitchRate = 0f;
                targetRollRate = 4.5f;
            }

            int sampleCount = Mathf.RoundToInt(ChunkLength / SampleSpacing);
            for (int i = 0; i <= sampleCount; i++)
            {
                chunk.samples.Add(cursor);
                chunk.ups.Add((frame * Vector3.up).normalized);
                if (i == sampleCount)
                {
                    break;
                }

                yawRate = Mathf.MoveTowards(yawRate, targetYawRate, 0.32f);
                pitchRate = Mathf.MoveTowards(pitchRate, targetPitchRate, 0.65f);
                rollRate = Mathf.MoveTowards(rollRate, targetRollRate, 0.9f);
                Quaternion localTurn = Quaternion.Euler(
                    pitchRate * SampleSpacing,
                    yawRate * SampleSpacing,
                    rollRate * SampleSpacing);
                frame = frame * localTurn;
                cursor += (frame * Vector3.forward).normalized * SampleSpacing;
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

                Vector3 up = chunk.ups[i].normalized;
                Vector3 right = Vector3.Cross(up, tangent.normalized).normalized;
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
            public List<Vector3> ups;
            public Vector3 boundsCenter;
        }
    }
}
