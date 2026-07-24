using UnityEngine;

namespace MusicRoad
{
    public sealed class VehicleSpec
    {
        public string DisplayName { get; }
        public string Category { get; }
        public bool CanNitro { get; }
        public float MaxSpeed { get; }
        public float NitroMaxSpeed { get; }
        public float ForwardAcceleration { get; }
        public float NitroAcceleration { get; }
        public float ReverseAcceleration { get; }
        public float LowSpeedTurnRate { get; }
        public float HighSpeedTurnRate { get; }
        public float Mass { get; }
        public Vector3 ColliderSize { get; }
        public Vector3 ColliderCenter { get; }
        public Vector3 VisualOffset { get; }

        public VehicleSpec(
            string displayName,
            string category,
            bool canNitro,
            float maxSpeed,
            float nitroMaxSpeed,
            float forwardAcceleration,
            float nitroAcceleration,
            float reverseAcceleration,
            float lowSpeedTurnRate,
            float highSpeedTurnRate,
            float mass,
            Vector3 colliderSize,
            Vector3 colliderCenter,
            Vector3 visualOffset)
        {
            DisplayName = displayName;
            Category = category;
            CanNitro = canNitro;
            MaxSpeed = maxSpeed;
            NitroMaxSpeed = nitroMaxSpeed;
            ForwardAcceleration = forwardAcceleration;
            NitroAcceleration = nitroAcceleration;
            ReverseAcceleration = reverseAcceleration;
            LowSpeedTurnRate = lowSpeedTurnRate;
            HighSpeedTurnRate = highSpeedTurnRate;
            Mass = mass;
            ColliderSize = colliderSize;
            ColliderCenter = colliderCenter;
            VisualOffset = visualOffset;
        }

        public int SpeedRating => Mathf.Clamp(Mathf.RoundToInt(MaxSpeed / 8f), 1, 5);
        public int HandlingRating => Mathf.Clamp(Mathf.RoundToInt(LowSpeedTurnRate / 23f), 1, 5);
        public string WeightLabel => Mass < 700f ? "LIGHT" : Mass < 1100f ? "MEDIUM" : "HEAVY";
    }

    public static class VehicleCatalog
    {
        public static readonly VehicleSpec[] All =
        {
            new VehicleSpec(
                "SPORT CAR", "TRACK", true,
                36f, 58f, 24f, 66f, 12f, 112f, 72f, 540f,
                new Vector3(1.9f, 0.75f, 3.35f), new Vector3(0f, 0.18f, 0f), new Vector3(0f, -0.42f, 0f)),
            new VehicleSpec(
                "MUSCLE CAR", "POWER", true,
                34f, 52f, 25f, 60f, 12f, 96f, 64f, 760f,
                new Vector3(2f, 0.85f, 3.6f), new Vector3(0f, 0.2f, 0f), new Vector3(0f, -0.43f, 0f)),
            new VehicleSpec(
                "HATCHBACK", "AGILE", false,
                30f, 30f, 21f, 21f, 12f, 116f, 76f, 650f,
                new Vector3(1.85f, 1f, 3.15f), new Vector3(0f, 0.3f, 0f), new Vector3(0f, -0.42f, 0f)),
            new VehicleSpec(
                "CLASSIC", "CRUISER", false,
                28f, 28f, 18f, 18f, 11f, 90f, 60f, 880f,
                new Vector3(1.95f, 0.9f, 3.55f), new Vector3(0f, 0.25f, 0f), new Vector3(0f, -0.43f, 0f)),
            new VehicleSpec(
                "PICKUP", "UTILITY", false,
                26f, 26f, 16f, 16f, 10f, 80f, 54f, 1280f,
                new Vector3(2.1f, 1.05f, 4f), new Vector3(0f, 0.32f, 0f), new Vector3(0f, -0.48f, 0f)),
            new VehicleSpec(
                "VAN", "HEAVY", false,
                23f, 23f, 14f, 14f, 9f, 74f, 50f, 1480f,
                new Vector3(2.05f, 1.55f, 3.8f), new Vector3(0f, 0.58f, 0f), new Vector3(0f, -0.46f, 0f)),
            new VehicleSpec(
                "MONSTER TRUCK", "OFFROAD", false,
                25f, 25f, 18f, 18f, 10f, 70f, 47f, 2200f,
                new Vector3(2.65f, 1.55f, 4.4f), new Vector3(0f, 0.58f, 0f), new Vector3(0f, -0.16f, 0f))
        };
    }
}
