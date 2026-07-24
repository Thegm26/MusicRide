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
        public float Climax => smoothed.heavy;
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
            RenderSettings.fogDensity = 0.019f;

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
            float hit = Mathf.Max(target.beat, target.onset * 0.92f);
            beatPulse = Mathf.Max(Mathf.Clamp01(hit * 1.12f), beatPulse - Time.unscaledDeltaTime * 5.8f);

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
            float energy = Mathf.Clamp01(smoothed.energy);
            float section = Mathf.Clamp01(smoothed.sectionLift);
            float vocalLift = Mathf.Clamp01(smoothed.vocalLift);
            float impact = Mathf.Clamp01(Mathf.Max(smoothed.onset, beatPulse));
            float harmonicChange = Mathf.Clamp01(smoothed.harmonicChange);
            float brightness = Mathf.Clamp01(smoothed.brightness);
            float hue = Mathf.Lerp(0.58f, 0.08f, brightness);
            Color spectral = Color.HSVToRGB(hue, 0.82f, 1f);
            Color opposite = Color.HSVToRGB(Mathf.Repeat(hue + 0.5f, 1f), 0.72f, 1f);
            Color sky = Color.Lerp(new Color(0.12f, 0.24f, 0.38f), new Color(0.48f, 0.7f, 0.9f), section);
            AccentColor = Color.Lerp(spectral, opposite, harmonicChange);

            Camera camera = Camera.main;
            if (camera != null)
            {
                camera.backgroundColor = Color.Lerp(camera.backgroundColor, sky, Time.unscaledDeltaTime * 1.8f);
            }

            Color fogTarget = Color.Lerp(new Color(0.14f, 0.2f, 0.27f), sky, 0.72f);
            RenderSettings.fogColor = Color.Lerp(RenderSettings.fogColor, fogTarget, Time.unscaledDeltaTime * 1.4f);
            RenderSettings.fogDensity = Mathf.Lerp(0.022f, 0.015f, section);
            Color ambient = Color.Lerp(new Color(0.42f, 0.46f, 0.5f), new Color(0.72f, 0.78f, 0.84f), section);
            RenderSettings.ambientLight = Color.Lerp(RenderSettings.ambientLight, ambient, Time.unscaledDeltaTime * 2f);

            if (sun != null)
            {
                float sunTarget = 0.82f + section * 1.15f + energy * 0.3f + impact * 0.48f;
                sun.intensity = Mathf.Lerp(sun.intensity, sunTarget, Time.unscaledDeltaTime * (impact > 0.7f ? 10f : 2f));
                sun.color = Color.Lerp(new Color(1f, 0.93f, 0.82f), Color.white, impact * 0.5f);
                sun.transform.rotation = Quaternion.Euler(42f + section * 12f, -35f + harmonicChange * 30f, 0f);
            }

            if (beatLight != null && camera != null)
            {
                beatLight.transform.position = camera.transform.position + camera.transform.forward * 11f + Vector3.up * 2f;
                beatLight.color = AccentColor;
                beatLight.intensity = beatPulse * 34f + vocalLift * 4f;
                beatLight.range = 25f + section * 18f + beatPulse * 24f;
            }

            if (edgeMaterial != null)
            {
                edgeMaterial.color = Color.Lerp(
                    Color.Lerp(Color.white, AccentColor, 0.2f + harmonicChange * 0.45f),
                    Color.white,
                    beatPulse);
                edgeMaterial.SetColor(
                    "_EmissionColor",
                    AccentColor * (0.35f + section * 1.2f + vocalLift * 1.5f + beatPulse * 7f));
            }

            if (environmentMaterial != null)
            {
                Color foliage = new Color(0.08f, 0.34f, 0.14f);
                environmentMaterial.color = foliage;
                environmentMaterial.SetColor("_EmissionColor", foliage * (0.08f + section * 0.12f));
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
                energy = 0.42f + Mathf.Sin(time * 0.7f) * 0.26f,
                vocal = 0.38f + Mathf.Sin(time * 0.41f + 0.8f) * 0.3f,
                vocalLift = 0.38f + Mathf.Sin(time * 0.41f + 0.8f) * 0.3f,
                percussion = beat,
                bass = 0.45f + Mathf.Sin(time * 0.53f) * 0.24f,
                mid = 0.4f + Mathf.Sin(time * 0.31f + 1.2f) * 0.22f,
                treble = 0.38f + Mathf.Sin(time * 1.13f + 2.5f) * 0.2f,
                brightness = 0.5f + Mathf.Sin(time * 0.17f) * 0.35f,
                onset = beat,
                beat = beat,
                lowImpact = beat * 0.8f,
                highImpact = beat * 0.65f,
                fullness = 0.58f,
                tonality = 0.72f,
                harmonicChange = 0.28f + beat * 0.45f,
                sectionLift = 0.48f + Mathf.Sin(time * 0.19f) * 0.28f,
                beatDensity = 0.55f,
                beatConfidence = 0.82f,
                bpm = 108f,
                heavy = 0.5f + beat * 0.35f,
                calibrationConfidence = 1f,
                profileSeconds = 60f
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
                energy = 0.04f,
                vocal = 0.02f,
                vocalLift = 0.02f,
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
