using System.Collections.Generic;
using UnityEngine;

namespace MusicRoad
{
    public sealed class RoadGenerator : MonoBehaviour
    {
        public const float RoadHalfWidth = 6.5f;
        private const float LandscapeHalfWidth = 72f;
        private const float ChunkLength = 20f;
        private const float SampleSpacing = 1f;
        private const int TargetChunkCount = 12;
        private const int EnvironmentInstancesPerChunk = 24;
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
        private Material mountainMaterial;
        private Material cloudMaterial;
        private Material landmarkMaterial;
        private GameObject[] environmentPrefabs;
        private Mesh mountainMesh;

        private Vector3 cursor = Vector3.zero;
        private float yaw;
        private float yawRate;
        private float slope;
        private int sequence;
        private float lastRetuneTime = float.NegativeInfinity;
        private float lastRetunedHardness = -1f;
        private bool hadAudibleMusic;
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
            mountainMaterial = CreateMaterialVariant(
                rockMaterial,
                "Distant Mountains",
                new Color(0.25f, 0.31f, 0.34f));
            cloudMaterial = CreateMaterialVariant(
                foliageMaterial,
                "Low Poly Clouds",
                new Color(0.82f, 0.88f, 0.92f));
            landmarkMaterial = CreateMaterialVariant(
                trunkMaterial,
                "World Landmarks",
                new Color(0.38f, 0.2f, 0.08f));
            mountainMesh = CreateMountainMesh();

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

            RetuneUnseenRoad(nearestChunk);
        }

        private void RetuneUnseenRoad(int nearestChunk)
        {
            AudioFeatureFrame features = music != null ? music.Immediate : default;
            bool audible = features.rawLevel > 0.0035f;
            float hardness = Mathf.Clamp01(features.heavy * 0.78f + features.lowImpact * 0.22f);
            bool firstSignal = audible && !hadAudibleMusic;
            bool intensityChanged =
                audible &&
                Time.unscaledTime - lastRetuneTime >= 2.5f &&
                Mathf.Abs(hardness - lastRetunedHardness) >= 0.1f;

            hadAudibleMusic = audible;
            if (!firstSignal && !intensityChanged)
            {
                return;
            }

            // The camera/fog horizon is shorter than this buffer, so a retune can
            // never replace geometry the player is currently looking at.
            int startIndex = Mathf.Clamp(nearestChunk + 6, 1, chunks.Count - 1);
            RoadChunk previous = chunks[startIndex - 1];
            cursor = previous.endCursor;
            yaw = previous.endYaw;
            yawRate = previous.endYawRate;
            slope = previous.endSlope;
            sequence = previous.sequenceIndex + 1;

            for (int i = startIndex; i < chunks.Count; i++)
            {
                PopulateChunk(chunks[i]);
            }

            lastRetuneTime = Time.unscaledTime;
            lastRetunedHardness = hardness;
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
            int startIndex = Mathf.RoundToInt(4f / SampleSpacing);
            return chunks.Count > 0 && chunks[0].samples.Count > startIndex
                ? chunks[0].samples[startIndex] + Vector3.up * 0.9f
                : new Vector3(0f, 0.9f, 4f);
        }

        public Quaternion GetStartRotation()
        {
            int startIndex = Mathf.RoundToInt(4f / SampleSpacing);
            if (chunks.Count == 0 || chunks[0].samples.Count <= startIndex + 1)
            {
                return Quaternion.identity;
            }

            Vector3 forward =
                chunks[0].samples[startIndex + 1] -
                chunks[0].samples[startIndex];
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
                sampleYaws = new List<float>(),
                sampleYawRates = new List<float>(),
                sampleSlopes = new List<float>(),
                environmentSlots = new List<Transform>(),
                mountainSlots = new List<Transform>(),
                hillSlots = new List<Transform>(),
                cloudSlots = new List<Transform>(),
                landmarkSlots = new List<Transform>()
            };

            if (environmentPrefabs != null && environmentPrefabs.Length > 0)
            {
                for (int i = 0; i < EnvironmentInstancesPerChunk; i++)
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
                    PrepareEnvironmentPhysics(instance);
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
            CreateWorldScenerySlots(chunk);
            return chunk;
        }

        private void PopulateChunk(RoadChunk chunk)
        {
            chunk.root.name = $"Road Chunk {sequence:0000}";
            chunk.samples.Clear();
            chunk.sampleYaws.Clear();
            chunk.sampleYawRates.Clear();
            chunk.sampleSlopes.Clear();
            chunk.sequenceIndex = sequence;

            AudioFeatureFrame features = music != null ? music.Immediate : default;
            float hardness = Mathf.Clamp01(features.heavy * 0.78f + features.lowImpact * 0.22f);
            float peakHardness = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.52f, 0.94f, hardness));
            float sideIntensity = Mathf.Clamp01(
                features.energy * 0.42f +
                features.heavy * 0.43f +
                features.beatDensity * 0.15f);
            float impactDrive = Mathf.Max(features.onset, features.lowImpact);
            float turnDrive = Mathf.Clamp01(
                features.harmonicChange * 0.35f +
                features.beatDensity * 0.2f +
                features.heavy * 0.3f +
                impactDrive * 0.15f);
            float hillDrive = Mathf.Clamp01(
                features.sectionLift * 0.28f +
                features.fullness * 0.12f +
                features.lowImpact * 0.22f +
                features.heavy * 0.38f);
            float plannedTurn = GetPlannedTurn(sequence);
            float plannedHill = GetPlannedHill(sequence);
            float turnStrength = Mathf.Lerp(
                0.72f,
                2.45f,
                Mathf.Clamp01(hardness * 0.55f + turnDrive * 0.45f));
            float targetYawRate =
                plannedTurn *
                turnStrength *
                Mathf.Lerp(1f, 1.12f, impactDrive);
            targetYawRate -= yaw * Mathf.Lerp(0.018f, 0.04f, sideIntensity);
            targetYawRate = Mathf.Clamp(targetYawRate, -2.45f, 2.45f);
            float targetSlope = plannedHill *
                Mathf.Lerp(0.16f, 0.62f, Mathf.Max(hillDrive, hardness)) *
                Mathf.Lerp(1f, 1.16f, impactDrive);
            targetSlope -= Mathf.Clamp(cursor.y * 0.065f, -0.55f, 0.55f);
            float slopeLimit = Mathf.Lerp(0.38f, 0.58f, peakHardness);
            targetSlope = Mathf.Clamp(targetSlope, -slopeLimit, slopeLimit);

            int sampleCount = Mathf.RoundToInt(ChunkLength / SampleSpacing);
            float bumpAmplitude = sequence < 2
                ? 0f
                : 0.04f + Mathf.Pow(hardness, 2.35f) * 1.45f;
            float bumpFrequency = Mathf.Lerp(0.48f, 0.92f, peakHardness);
            for (int i = 0; i <= sampleCount; i++)
            {
                chunk.samples.Add(cursor);
                chunk.sampleYaws.Add(yaw);
                chunk.sampleYawRates.Add(yawRate);
                chunk.sampleSlopes.Add(slope);
                if (i == sampleCount)
                {
                    break;
                }

                float responseScale = SampleSpacing * 0.5f;
                yawRate = Mathf.MoveTowards(
                    yawRate,
                    targetYawRate,
                    Mathf.Lerp(0.12f, 0.62f, peakHardness) * responseScale);
                slope = Mathf.MoveTowards(
                    slope,
                    targetSlope,
                    Mathf.Lerp(0.024f, 0.16f, peakHardness) * responseScale);
                float nextYaw = yaw + yawRate * SampleSpacing;
                if (Mathf.Abs(nextYaw) > 52f)
                {
                    yaw = Mathf.Clamp(nextYaw, -52f, 52f);
                    yawRate = -Mathf.Sign(yaw) * Mathf.Min(Mathf.Abs(yawRate) * 0.35f, 0.75f);
                }
                else
                {
                    yaw = nextYaw;
                }
                Vector3 forward = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
                float globalSample = GetNoiseSample(sequence, i);
                float noiseStep = SampleSpacing * 0.5f;
                float bumpNow = RoadBump(globalSample, bumpFrequency, bumpAmplitude);
                float bumpNext = RoadBump(globalSample + noiseStep, bumpFrequency, bumpAmplitude);
                float verticalStep = Mathf.Clamp(
                    slope * SampleSpacing + bumpNext - bumpNow,
                    -0.6f,
                    0.6f);
                float nextHeight = cursor.y + verticalStep;
                if (nextHeight > 14f || nextHeight < -4f)
                {
                    float limitedHeight = Mathf.Clamp(nextHeight, -4f, 14f);
                    verticalStep = limitedHeight - cursor.y;
                    slope = nextHeight > 14f
                        ? Mathf.Min(slope, -0.08f)
                        : Mathf.Max(slope, 0.08f);
                }
                cursor += forward * SampleSpacing +
                    Vector3.up * verticalStep;
            }

            BuildMesh(chunk);
            PopulateEnvironment(chunk);
            PopulateWorldScenery(chunk);
            chunk.boundsCenter = chunk.samples[chunk.samples.Count / 2];
            chunk.endCursor = cursor;
            chunk.endYaw = yaw;
            chunk.endYawRate = yawRate;
            chunk.endSlope = slope;
            sequence++;
        }

        private float RoadBump(float sample, float frequency, float amplitude)
        {
            float primary = Mathf.Sin((sample + seed) * frequency) * 0.72f;
            float detail = Mathf.Sin(sample * frequency * 2.37f + seed * 1.91f) * 0.28f;
            return (primary + detail) * amplitude;
        }

        private static float GetNoiseSample(int sequenceIndex, int sampleIndex)
        {
            return (sequenceIndex * ChunkLength + sampleIndex * SampleSpacing) * 0.5f;
        }

        private float GetPlannedTurn(int sequenceIndex)
        {
            // A fixed opening teaches the route immediately: straight, right,
            // cross through center, then hold a progressively stronger left.
            switch (sequenceIndex)
            {
                case 0:
                case 1:
                    return 0f;
                case 2:
                    return 0.42f;
                case 3:
                    return 0.78f;
                case 4:
                    return 0.34f;
                case 5:
                    return -0.42f;
                case 6:
                    return -0.72f;
                case 7:
                    return -1f;
                case 8:
                    return -0.68f;
            }

            // Beyond the opening, deterministic three-chunk control points form
            // a repeatable multi-minute route. Music scales these controls but
            // never changes their turn order or invents a new topology.
            float routePosition = (sequenceIndex - 9) / 3f;
            int key = Mathf.FloorToInt(routePosition);
            float blend = Mathf.SmoothStep(0f, 1f, routePosition - key);
            return Mathf.Lerp(GetSeededTurnKey(key), GetSeededTurnKey(key + 1), blend);
        }

        private float GetSeededTurnKey(int key)
        {
            int pattern = Mathf.Abs(key) % 6;
            float direction = pattern == 0 || pattern == 1 || pattern == 5 ? 1f : -1f;
            float variation = Mathf.PerlinNoise(seed + key * 0.371f, seed * 0.193f);
            return direction * Mathf.Lerp(0.46f, 1f, variation);
        }

        private float GetPlannedHill(int sequenceIndex)
        {
            if (sequenceIndex < 2)
            {
                return 0f;
            }

            float routePosition = (sequenceIndex - 2) / 4f;
            int key = Mathf.FloorToInt(routePosition);
            float blend = Mathf.SmoothStep(0f, 1f, routePosition - key);
            return Mathf.Lerp(GetSeededHillKey(key), GetSeededHillKey(key + 1), blend);
        }

        private float GetSeededHillKey(int key)
        {
            int pattern = Mathf.Abs(key) % 4;
            float direction = pattern < 2 ? 1f : -1f;
            float variation = Mathf.PerlinNoise(seed * 0.127f, seed + key * 0.419f);
            return direction * Mathf.Lerp(0.36f, 1f, variation);
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
                int forestLayer = (i / 2) % 4;
                float distance = sign
                    ? Random.Range(8.2f, 9.5f)
                    : smallFoliage
                        ? Random.Range(8.5f, 15f)
                        : tree
                            ? 14f + forestLayer * 11f + Random.Range(0f, 6f)
                            : Random.Range(10f, 28f);
                float globalSample = GetNoiseSample(chunk.sequenceIndex, sampleIndex);
                float groundOffset = TerrainHeightOffset(globalSample, side, distance);
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

        private float TerrainHeightOffset(float globalSample, float side, float distance)
        {
            float landscapeAmount = Mathf.InverseLerp(RoadHalfWidth, LandscapeHalfWidth, distance);
            float sideSeed = side < 0f ? 11.7f : 37.3f;
            float broad = (Mathf.PerlinNoise(globalSample * 0.038f + sideSeed, seed * 0.13f) - 0.5f) * 10f;
            float detail = (Mathf.PerlinNoise(globalSample * 0.11f, sideSeed + seed) - 0.5f) * 2.4f;
            return (broad + detail - 0.35f) * landscapeAmount;
        }

        private void CreateWorldScenerySlots(RoadChunk chunk)
        {
            for (int i = 0; i < 2; i++)
            {
                GameObject mountain = new GameObject($"MOUNTAIN_SLOT_{i + 1:00}");
                mountain.transform.SetParent(chunk.root.transform, false);
                mountain.AddComponent<MeshFilter>().sharedMesh = mountainMesh;
                mountain.AddComponent<MeshRenderer>().sharedMaterial = mountainMaterial;
                mountain.AddComponent<MeshCollider>().sharedMesh = mountainMesh;
                chunk.mountainSlots.Add(mountain.transform);

                GameObject hill = CreateEnvironmentPrimitive(
                    chunk.root.transform,
                    $"DISTANT_HILL_SLOT_{i + 1:00}",
                    PrimitiveType.Sphere,
                    Vector3.zero,
                    Vector3.one,
                    foliageMaterial,
                    true);
                chunk.hillSlots.Add(hill.transform);
            }

            GameObject cloud = CreateEnvironmentPrimitive(
                chunk.root.transform,
                "CLOUD_SLOT",
                PrimitiveType.Sphere,
                Vector3.zero,
                Vector3.one,
                cloudMaterial);
            chunk.cloudSlots.Add(cloud.transform);

            GameObject landmark = new GameObject("CABIN_LANDMARK_SLOT");
            landmark.transform.SetParent(chunk.root.transform, false);
            CreateEnvironmentPrimitive(
                landmark.transform,
                "Cabin Body",
                PrimitiveType.Cube,
                new Vector3(0f, 1.4f, 0f),
                new Vector3(4.8f, 2.8f, 4f),
                landmarkMaterial,
                true);
            GameObject leftRoof = CreateEnvironmentPrimitive(
                landmark.transform,
                "Cabin Roof Left",
                PrimitiveType.Cube,
                new Vector3(-1.05f, 3.15f, 0f),
                new Vector3(3.1f, 0.35f, 4.8f),
                rockMaterial,
                true);
            leftRoof.transform.localRotation = Quaternion.Euler(0f, 0f, 27f);
            GameObject rightRoof = CreateEnvironmentPrimitive(
                landmark.transform,
                "Cabin Roof Right",
                PrimitiveType.Cube,
                new Vector3(1.05f, 3.15f, 0f),
                new Vector3(3.1f, 0.35f, 4.8f),
                rockMaterial,
                true);
            rightRoof.transform.localRotation = Quaternion.Euler(0f, 0f, -27f);
            CreateEnvironmentPrimitive(
                landmark.transform,
                "Cabin Door",
                PrimitiveType.Cube,
                new Vector3(0f, 1.05f, -2.03f),
                new Vector3(1.05f, 2.1f, 0.12f),
                trunkMaterial,
                true);
            chunk.landmarkSlots.Add(landmark.transform);
        }

        private void PopulateWorldScenery(RoadChunk chunk)
        {
            for (int i = 0; i < chunk.mountainSlots.Count; i++)
            {
                int sampleIndex = Mathf.Clamp(
                    Mathf.RoundToInt(Mathf.Lerp(2f, chunk.samples.Count - 3f, 0.28f + i * 0.44f)),
                    1,
                    chunk.samples.Count - 2);
                Vector3 tangent = (chunk.samples[sampleIndex + 1] - chunk.samples[sampleIndex - 1]).normalized;
                Vector3 right = Vector3.Cross(Vector3.up, tangent).normalized;
                float side = (sequence + i) % 2 == 0 ? -1f : 1f;
                float variation = Mathf.PerlinNoise(sequence * 0.21f + i * 0.37f, seed);
                float distance = 78f + variation * 20f;
                float globalSample = GetNoiseSample(chunk.sequenceIndex, sampleIndex);
                float groundOffset = TerrainHeightOffset(globalSample, side, distance);
                Transform mountain = chunk.mountainSlots[i];
                mountain.position =
                    chunk.samples[sampleIndex] +
                    right * side * distance +
                    Vector3.up * (groundOffset - 2.5f);
                mountain.rotation = Quaternion.Euler(0f, yaw + variation * 80f, 0f);
                mountain.localScale = new Vector3(
                    14f + variation * 10f,
                    22f + variation * 18f,
                    12f + variation * 9f);
            }

            for (int i = 0; i < chunk.hillSlots.Count; i++)
            {
                int sampleIndex = i == 0 ? 3 : chunk.samples.Count - 4;
                Vector3 tangent = (chunk.samples[sampleIndex + 1] - chunk.samples[sampleIndex - 1]).normalized;
                Vector3 right = Vector3.Cross(Vector3.up, tangent).normalized;
                float side = (sequence + i + 1) % 2 == 0 ? -1f : 1f;
                float variation = Mathf.PerlinNoise(sequence * 0.17f, seed + i * 7.1f);
                float distance = 38f + variation * 20f;
                float globalSample = GetNoiseSample(chunk.sequenceIndex, sampleIndex);
                float groundOffset = TerrainHeightOffset(globalSample, side, distance);
                Transform hill = chunk.hillSlots[i];
                hill.position =
                    chunk.samples[sampleIndex] +
                    right * side * distance +
                    Vector3.up * (groundOffset - 3f);
                hill.localScale = new Vector3(
                    18f + variation * 10f,
                    5f + variation * 5f,
                    13f + variation * 8f);
            }

            for (int i = 0; i < chunk.cloudSlots.Count; i++)
            {
                int sampleIndex = chunk.samples.Count / 2;
                Vector3 tangent = (chunk.samples[sampleIndex + 1] - chunk.samples[sampleIndex - 1]).normalized;
                Vector3 right = Vector3.Cross(Vector3.up, tangent).normalized;
                float side = sequence % 2 == 0 ? -1f : 1f;
                float variation = Mathf.PerlinNoise(sequence * 0.19f, seed + 83f);
                Transform cloud = chunk.cloudSlots[i];
                cloud.position =
                    chunk.samples[sampleIndex] +
                    right * side * (18f + variation * 28f) +
                    Vector3.up * (24f + variation * 16f);
                cloud.localScale = new Vector3(
                    7f + variation * 7f,
                    1.8f + variation * 1.4f,
                    4f + variation * 4f);
            }

            for (int i = 0; i < chunk.landmarkSlots.Count; i++)
            {
                Transform landmark = chunk.landmarkSlots[i];
                bool visible = sequence > 1 && sequence % 4 == 1;
                landmark.gameObject.SetActive(visible);
                if (!visible)
                {
                    continue;
                }

                int sampleIndex = chunk.samples.Count / 2;
                Vector3 tangent = (chunk.samples[sampleIndex + 1] - chunk.samples[sampleIndex - 1]).normalized;
                Vector3 right = Vector3.Cross(Vector3.up, tangent).normalized;
                float side = sequence % 8 < 4 ? -1f : 1f;
                float distance = 19f;
                float globalSample = GetNoiseSample(chunk.sequenceIndex, sampleIndex);
                landmark.position =
                    chunk.samples[sampleIndex] +
                    right * side * distance +
                    Vector3.up * TerrainHeightOffset(globalSample, side, distance);
                landmark.rotation = Quaternion.LookRotation(-right * side, Vector3.up);
                landmark.localScale = Vector3.one * (0.8f + Mathf.PerlinNoise(sequence, seed) * 0.35f);
            }
        }

        private static Material CreateMaterialVariant(Material source, string name, Color color)
        {
            Material material = new Material(source)
            {
                name = name,
                color = color
            };
            if (material.HasProperty("_EmissionColor"))
            {
                material.SetColor("_EmissionColor", color * 0.08f);
            }
            return material;
        }

        private static Mesh CreateMountainMesh()
        {
            const int sides = 8;
            var vertices = new Vector3[sides + 2];
            var triangles = new int[sides * 6];
            vertices[0] = new Vector3(0f, 1f, 0f);
            vertices[sides + 1] = Vector3.zero;
            for (int i = 0; i < sides; i++)
            {
                float angle = i * Mathf.PI * 2f / sides;
                vertices[i + 1] = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                int next = (i + 1) % sides;
                int triangle = i * 6;
                triangles[triangle] = 0;
                triangles[triangle + 1] = i + 1;
                triangles[triangle + 2] = next + 1;
                triangles[triangle + 3] = sides + 1;
                triangles[triangle + 4] = next + 1;
                triangles[triangle + 5] = i + 1;
            }

            Mesh mesh = new Mesh { name = "Low Poly Mountain" };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void PrepareEnvironmentPhysics(GameObject environment)
        {
            string itemName = environment.name.ToLowerInvariant();
            bool decorative =
                itemName.Contains("grass") ||
                itemName.Contains("mushroom");
            Rigidbody[] bodies = environment.GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < bodies.Length; i++)
            {
                Destroy(bodies[i]);
            }

            Collider[] colliders = environment.GetComponentsInChildren<Collider>(true);
            if (decorative)
            {
                for (int i = 0; i < colliders.Length; i++)
                {
                    Destroy(colliders[i]);
                }
                return;
            }

            if (colliders.Length > 0)
            {
                for (int i = 0; i < colliders.Length; i++)
                {
                    colliders[i].enabled = true;
                    colliders[i].isTrigger = false;
                }
                return;
            }

            Renderer[] renderers = environment.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return;
            }

            Bounds worldBounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                worldBounds.Encapsulate(renderers[i].bounds);
            }

            Transform root = environment.transform;
            Vector3 center = root.InverseTransformPoint(worldBounds.center);
            Vector3 size = Abs(root.InverseTransformVector(worldBounds.size));
            bool tree = itemName.Contains("tree") && !itemName.Contains("stump");

            if (tree)
            {
                CapsuleCollider trunk = environment.AddComponent<CapsuleCollider>();
                trunk.direction = 1;
                trunk.center = new Vector3(center.x, size.y * 0.42f, center.z);
                trunk.height = Mathf.Max(1.2f, size.y * 0.78f);
                trunk.radius = Mathf.Clamp(Mathf.Min(size.x, size.z) * 0.14f, 0.22f, 0.85f);
                return;
            }

            BoxCollider box = environment.AddComponent<BoxCollider>();
            box.center = center;
            box.size = Vector3.Max(size * 0.88f, Vector3.one * 0.25f);
        }

        private static Vector3 Abs(Vector3 value)
        {
            return new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
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
                trunkMaterial,
                true);
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
                rockMaterial,
                true);
            return root.transform;
        }

        private static GameObject CreateEnvironmentPrimitive(
            Transform parent,
            string name,
            PrimitiveType type,
            Vector3 localPosition,
            Vector3 localScale,
            Material material,
            bool solid = false)
        {
            GameObject primitive = GameObject.CreatePrimitive(type);
            primitive.name = name;
            primitive.transform.SetParent(parent, false);
            primitive.transform.localPosition = localPosition;
            primitive.transform.localScale = localScale;
            primitive.GetComponent<Renderer>().sharedMaterial = material;
            Collider collider = primitive.GetComponent<Collider>();
            if (collider != null && !solid)
            {
                Destroy(collider);
            }
            return primitive;
        }

        private void BuildMesh(RoadChunk chunk)
        {
            const int verticesPerRow = 12;
            int rowCount = chunk.samples.Count;
            var vertices = new Vector3[rowCount * verticesPerRow];
            var uvs = new Vector2[vertices.Length];
            var shoulderTriangles = new List<int>();
            var roadTriangles = new List<int>();
            var edgeTriangles = new List<int>();
            float[] offsets =
            {
                -LandscapeHalfWidth,
                -42f,
                -18f,
                -RoadHalfWidth,
                -RoadHalfWidth + 0.22f,
                -0.11f,
                0.11f,
                RoadHalfWidth - 0.22f,
                RoadHalfWidth,
                18f,
                42f,
                LandscapeHalfWidth
            };

            for (int i = 0; i < rowCount; i++)
            {
                Vector3 planarForward =
                    Quaternion.Euler(0f, chunk.sampleYaws[i], 0f) *
                    Vector3.forward;
                Vector3 tangent =
                    (planarForward + Vector3.up * chunk.sampleSlopes[i]).normalized;
                Vector3 flatRight = Vector3.Cross(Vector3.up, planarForward).normalized;
                float bankAngle = Mathf.Clamp(-chunk.sampleYawRates[i] * 9f, -24f, 24f);
                for (int column = 0; column < verticesPerRow; column++)
                {
                    int index = i * verticesPerRow + column;
                    float side = Mathf.Sign(offsets[column]);
                    float distance = Mathf.Abs(offsets[column]);
                    float bankBlend = 1f - Mathf.InverseLerp(RoadHalfWidth, 18f, distance);
                    Vector3 right =
                        Quaternion.AngleAxis(bankAngle * bankBlend, tangent) *
                        flatRight;
                    float globalSample = GetNoiseSample(chunk.sequenceIndex, i);
                    float terrainHeight = TerrainHeightOffset(globalSample, side, distance);
                    vertices[index] =
                        chunk.samples[i] +
                        right * offsets[column] +
                        Vector3.up * terrainHeight;
                    uvs[index] = new Vector2(
                        column / (verticesPerRow - 1f),
                        (chunk.sequenceIndex * ChunkLength + i * SampleSpacing) * 0.08f);
                }
            }

            for (int row = 0; row < rowCount - 1; row++)
            {
                for (int column = 0; column < verticesPerRow - 1; column++)
                {
                    if (column <= 2 || column >= 8)
                    {
                        AddStrip(shoulderTriangles, row, column, verticesPerRow);
                    }
                    else if (column == 3 || column == 5 || column == 7)
                    {
                        AddStrip(edgeTriangles, row, column, verticesPerRow);
                    }
                    else
                    {
                        AddStrip(roadTriangles, row, column, verticesPerRow);
                    }
                }
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
            public List<float> sampleYaws;
            public List<float> sampleYawRates;
            public List<float> sampleSlopes;
            public List<Transform> environmentSlots;
            public List<Transform> mountainSlots;
            public List<Transform> hillSlots;
            public List<Transform> cloudSlots;
            public List<Transform> landmarkSlots;
            public Vector3 boundsCenter;
            public int sequenceIndex;
            public Vector3 endCursor;
            public float endYaw;
            public float endYawRate;
            public float endSlope;
        }
    }
}
