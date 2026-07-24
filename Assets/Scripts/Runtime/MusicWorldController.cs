using System.Collections.Generic;
using UnityEngine;

namespace MusicRoad
{
    public sealed class MusicWorldController : MonoBehaviour
    {
        private const float RoadDelaySeconds = 4f;
        private readonly Queue<TimedFrame> history = new Queue<TimedFrame>();

        private AudioCaptureService capture;
        private Light sun;
        private Light beatLight;
        private Material edgeMaterial;
        private Material environmentMaterial;
        private AudioFeatureFrame smoothed;
        private AudioFeatureFrame delayed;
        private float beatPulse;

        public AudioFeatureFrame Immediate => smoothed;
        public AudioFeatureFrame Delayed => delayed;
        public float BeatPulse => beatPulse;
        public Color AccentColor { get; private set; } = new Color(0.15f, 0.9f, 1f);

        private struct TimedFrame
        {
            public float time;
            public AudioFeatureFrame frame;
        }

        public void Initialize(
            AudioCaptureService audioCapture,
            Light directionalLight,
            Material edge,
            Material environment)
        {
            capture = audioCapture;
            sun = directionalLight;
            edgeMaterial = edge;
            environmentMaterial = environment;
            capture.FeaturesReceived += OnFeatures;
            smoothed = CreateIdleFrame(0f);
            delayed = smoothed;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.012f;

            GameObject beatLightObject = new GameObject("Music Beat Flash");
            beatLightObject.transform.SetParent(transform, false);
            beatLight = beatLightObject.AddComponent<Light>();
            beatLight.type = LightType.Point;
            beatLight.range = 38f;
            beatLight.shadows = LightShadows.None;
            beatLight.cullingMask &= ~(1 << MusicRoadBootstrap.VehicleRenderLayer);
        }

        private void OnFeatures(AudioFeatureFrame frame)
        {
            history.Enqueue(new TimedFrame { time = Time.unscaledTime, frame = frame });
        }

        private void Update()
        {
            AudioFeatureFrame target = capture != null && capture.State == AudioCaptureState.Active
                ? capture.LatestFeatures
                : CreateIdleFrame(Time.unscaledTime);

            float blend = 1f - Mathf.Exp(-Time.unscaledDeltaTime * 4.5f);
            smoothed = AudioFeatureFrame.Lerp(smoothed, target, blend);
            float hit = Mathf.Max(target.beat, target.percussion * 0.9f);
            beatPulse = Mathf.Max(Mathf.Clamp01(hit * 1.15f), beatPulse - Time.unscaledDeltaTime * 4.2f);

            if (capture == null || capture.State != AudioCaptureState.Active)
            {
                history.Enqueue(new TimedFrame { time = Time.unscaledTime, frame = target });
            }

            float cutoff = Time.unscaledTime - RoadDelaySeconds;
            while (history.Count > 0 && history.Peek().time <= cutoff)
            {
                delayed = history.Dequeue().frame;
            }

            while (history.Count > 240)
            {
                history.Dequeue();
            }

            ApplyPresentation();
        }

        private void ApplyPresentation()
        {
            float energy = Mathf.Clamp01(smoothed.intensity * 1.35f);
            float vocal = Mathf.Clamp01(smoothed.vocal * 1.45f);
            float percussion = Mathf.Clamp01(Mathf.Max(smoothed.percussion, beatPulse));
            float brightness = Mathf.Clamp01(smoothed.brightness * 1.35f);
            float paletteStep = Mathf.Floor((brightness * 4f + vocal * 4f) % 6f) / 6f;
            float hue = Mathf.Repeat(0.55f + paletteStep + vocal * 0.28f, 1f);
            Color spectral = Color.HSVToRGB(hue, 0.88f, Mathf.Lerp(0.35f, 1f, energy));
            Color opposite = Color.HSVToRGB(Mathf.Repeat(hue + 0.5f, 1f), 0.92f, 1f);
            Color sky = Color.Lerp(spectral * 0.25f, opposite, vocal * 0.88f);
            sky = Color.Lerp(sky, Color.white, beatPulse * 0.88f);
            AccentColor = Color.Lerp(spectral, opposite, percussion);

            Camera camera = Camera.main;
            if (camera != null)
            {
                float skySpeed = 3.5f + vocal * 8f + percussion * 22f;
                camera.backgroundColor = Color.Lerp(camera.backgroundColor, sky, Time.unscaledDeltaTime * skySpeed);
            }

            RenderSettings.fogColor = Color.Lerp(RenderSettings.fogColor, opposite * 0.48f, Time.unscaledDeltaTime * 5f);
            float baseFogDensity = Mathf.Lerp(0.016f, 0.0065f, Mathf.Max(energy, vocal));
            RenderSettings.fogDensity = baseFogDensity * Mathf.Lerp(1f, 0.42f, beatPulse);
            Color ambient = Color.Lerp(new Color(0.025f, 0.03f, 0.06f), spectral * 1.25f, energy * 0.6f + vocal * 0.4f);
            RenderSettings.ambientLight = Color.Lerp(ambient, Color.white * 1.35f, beatPulse * 0.72f);

            if (sun != null)
            {
                sun.intensity = 0.12f + energy * 1.9f + vocal * 3.2f + percussion * 3.8f + beatPulse * 7f;
                sun.color = Color.Lerp(Color.Lerp(spectral, opposite, vocal), Color.white, beatPulse * 0.82f);
                sun.transform.rotation = Quaternion.Euler(18f + vocal * 68f, -90f + brightness * 180f, percussion * 18f);
            }

            if (beatLight != null && camera != null)
            {
                beatLight.transform.position = camera.transform.position + camera.transform.forward * 11f + Vector3.up * 2f;
                beatLight.color = opposite;
                beatLight.intensity = vocal * 8f + percussion * 20f + beatPulse * 46f;
                beatLight.range = 28f + energy * 34f + beatPulse * 34f;
            }

            if (edgeMaterial != null)
            {
                edgeMaterial.color = Color.Lerp(
                    Color.Lerp(Color.white, AccentColor, 0.18f + vocal * 0.32f),
                    Color.white,
                    beatPulse);
                edgeMaterial.SetColor(
                    "_EmissionColor",
                    AccentColor * (0.2f + vocal * 0.75f + percussion * 1.4f + beatPulse * 8f));
            }

            if (environmentMaterial != null)
            {
                Color foliage = new Color(0.08f, 0.34f, 0.14f);
                environmentMaterial.color = Color.Lerp(foliage, AccentColor, beatPulse * 0.72f);
                environmentMaterial.SetColor("_EmissionColor", AccentColor * (beatPulse * 4.5f));
            }
        }

        private static AudioFeatureFrame CreateDemoFrame(float time)
        {
            float beatPhase = Mathf.Repeat(time * 1.8f, 1f);
            float beat = beatPhase < 0.08f ? 1f - beatPhase / 0.08f : 0f;
            return new AudioFeatureFrame
            {
                timestamp = time,
                rms = 0.35f + Mathf.Sin(time * 0.7f) * 0.16f,
                intensity = 0.42f + Mathf.Sin(time * 0.7f) * 0.26f,
                vocal = 0.38f + Mathf.Sin(time * 0.41f + 0.8f) * 0.3f,
                percussion = beat,
                bass = 0.45f + Mathf.Sin(time * 0.53f) * 0.24f,
                mid = 0.4f + Mathf.Sin(time * 0.31f + 1.2f) * 0.22f,
                treble = 0.38f + Mathf.Sin(time * 1.13f + 2.5f) * 0.2f,
                brightness = 0.5f + Mathf.Sin(time * 0.17f) * 0.35f,
                onset = beat,
                beat = beat
            };
        }

        private static AudioFeatureFrame CreateIdleFrame(float time)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return new AudioFeatureFrame
            {
                timestamp = time,
                rms = 0.04f,
                intensity = 0.04f,
                vocal = 0.02f,
                percussion = 0f,
                bass = 0.05f,
                mid = 0.04f,
                treble = 0.03f,
                brightness = 0.18f
            };
#else
            return CreateDemoFrame(time);
#endif
        }
    }
}
