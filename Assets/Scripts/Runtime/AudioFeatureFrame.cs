using System;
using UnityEngine;

namespace MusicRoad
{
    [Serializable]
    public struct AudioFeatureFrame
    {
        public float timestamp;
        [Range(0f, 1f)] public float rms;
        [Range(0f, 1f)] public float intensity;
        [Range(0f, 1f)] public float vocal;
        [Range(0f, 1f)] public float percussion;
        [Range(0f, 1f)] public float bass;
        [Range(0f, 1f)] public float mid;
        [Range(0f, 1f)] public float treble;
        [Range(0f, 1f)] public float brightness;
        [Range(0f, 1f)] public float onset;
        [Range(0f, 1f)] public float beat;

        public static AudioFeatureFrame Lerp(AudioFeatureFrame a, AudioFeatureFrame b, float t)
        {
            return new AudioFeatureFrame
            {
                timestamp = Mathf.Lerp(a.timestamp, b.timestamp, t),
                rms = Mathf.Lerp(a.rms, b.rms, t),
                intensity = Mathf.Lerp(a.intensity, b.intensity, t),
                vocal = Mathf.Lerp(a.vocal, b.vocal, t),
                percussion = Mathf.Lerp(a.percussion, b.percussion, t),
                bass = Mathf.Lerp(a.bass, b.bass, t),
                mid = Mathf.Lerp(a.mid, b.mid, t),
                treble = Mathf.Lerp(a.treble, b.treble, t),
                brightness = Mathf.Lerp(a.brightness, b.brightness, t),
                onset = Mathf.Lerp(a.onset, b.onset, t),
                beat = Mathf.Lerp(a.beat, b.beat, t)
            };
        }
    }

    public enum AudioCaptureState
    {
        Disconnected,
        Requesting,
        Active,
        NoAudio,
        Denied,
        Ended,
        Unsupported
    }
}
