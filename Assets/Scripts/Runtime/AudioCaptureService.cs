using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace MusicRoad
{
    public sealed class AudioCaptureService : MonoBehaviour
    {
        public event Action<AudioFeatureFrame> FeaturesReceived;
        public event Action<AudioCaptureState, string> StateChanged;

        public AudioCaptureState State { get; private set; } = AudioCaptureState.Disconnected;
        public AudioFeatureFrame LatestFeatures { get; private set; }
        public bool HasReceivedFeatures { get; private set; }
        public float SecondsSinceLastFeatures => HasReceivedFeatures
            ? Time.unscaledTime - lastFeatureTime
            : float.PositiveInfinity;

        private float lastFeatureTime;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void MusicRoad_PrepareCapture();

        [DllImport("__Internal")]
        private static extern void MusicRoad_StartCapture();

        [DllImport("__Internal")]
        private static extern void MusicRoad_StopCapture();
#endif

        private void Awake()
        {
            gameObject.name = "AudioCaptureService";
#if UNITY_WEBGL && !UNITY_EDITOR
            MusicRoad_PrepareCapture();
#endif
        }

        public void StartCapture()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            HasReceivedFeatures = false;
            LatestFeatures = default;
            SetState(AudioCaptureState.Requesting, "Share a screen with system audio, or share the music tab with tab audio.");
            MusicRoad_StartCapture();
#else
            SetState(AudioCaptureState.Unsupported, "Browser capture is available in the WebGL build. Demo music is active in the Editor.");
#endif
        }

        public void StopCapture()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            MusicRoad_StopCapture();
#endif
            HasReceivedFeatures = false;
            LatestFeatures = default;
            SetState(AudioCaptureState.Ended, "Computer audio capture stopped.");
        }

        // Called from Assets/Plugins/WebGL/MusicCapture.jslib.
        public void OnAudioFeatures(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return;
            }

            try
            {
                LatestFeatures = JsonUtility.FromJson<AudioFeatureFrame>(json);
                HasReceivedFeatures = true;
                lastFeatureTime = Time.unscaledTime;
                FeaturesReceived?.Invoke(LatestFeatures);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Could not parse browser audio features: {exception.Message}");
            }
        }

        // Called from Assets/Plugins/WebGL/MusicCapture.jslib as "state|message".
        public void OnCaptureState(string payload)
        {
            string[] pieces = payload.Split(new[] { '|' }, 2);
            if (!Enum.TryParse(pieces[0], true, out AudioCaptureState state))
            {
                state = AudioCaptureState.Disconnected;
            }

            string message = pieces.Length > 1 ? pieces[1] : string.Empty;
            SetState(state, message);
        }

        private void SetState(AudioCaptureState state, string message)
        {
            State = state;
            StateChanged?.Invoke(state, message);
        }

        private void OnDestroy()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            MusicRoad_StopCapture();
#endif
        }
    }
}
