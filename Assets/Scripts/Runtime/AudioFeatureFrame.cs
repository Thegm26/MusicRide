using System;
using UnityEngine;

namespace MusicRoad
{
    [Serializable]
    public struct AudioFeatureFrame
    {
        public float timestamp;
        [Range(0f, 1f)] public float rms;
        public float rawLevel;
        public float perceivedLoudness;
        [Range(0f, 1f)] public float intensity;
        [Range(0f, 1f)] public float energy;
        [Range(0f, 1f)] public float vocal;
        [Range(0f, 1f)] public float vocalLift;
        [Range(0f, 1f)] public float percussion;
        [Range(0f, 1f)] public float bass;
        [Range(0f, 1f)] public float mid;
        [Range(0f, 1f)] public float treble;
        [Range(0f, 1f)] public float brightness;
        [Range(0f, 1f)] public float onset;
        [Range(0f, 1f)] public float beat;
        [Range(0f, 1f)] public float lowImpact;
        [Range(0f, 1f)] public float highImpact;
        [Range(0f, 1f)] public float fullness;
        [Range(0f, 1f)] public float sharpness;
        [Range(0f, 1f)] public float tonality;
        [Range(0f, 1f)] public float harmonicChange;
        [Range(0f, 1f)] public float sectionLift;
        [Range(0f, 1f)] public float beatDensity;
        [Range(0f, 1f)] public float beatConfidence;
        public float bpm;
        [Range(0f, 1f)] public float heavy;
        [Range(0f, 1f)] public float calibrationConfidence;
        public float profileSeconds;

        public static AudioFeatureFrame Lerp(AudioFeatureFrame a, AudioFeatureFrame b, float t)
        {
            return new AudioFeatureFrame
            {
                timestamp = Mathf.Lerp(a.timestamp, b.timestamp, t),
                rms = Mathf.Lerp(a.rms, b.rms, t),
                rawLevel = Mathf.Lerp(a.rawLevel, b.rawLevel, t),
                perceivedLoudness = Mathf.Lerp(a.perceivedLoudness, b.perceivedLoudness, t),
                intensity = Mathf.Lerp(a.intensity, b.intensity, t),
                energy = Mathf.Lerp(a.energy, b.energy, t),
                vocal = Mathf.Lerp(a.vocal, b.vocal, t),
                vocalLift = Mathf.Lerp(a.vocalLift, b.vocalLift, t),
                percussion = Mathf.Lerp(a.percussion, b.percussion, t),
                bass = Mathf.Lerp(a.bass, b.bass, t),
                mid = Mathf.Lerp(a.mid, b.mid, t),
                treble = Mathf.Lerp(a.treble, b.treble, t),
                brightness = Mathf.Lerp(a.brightness, b.brightness, t),
                onset = Mathf.Lerp(a.onset, b.onset, t),
                beat = Mathf.Lerp(a.beat, b.beat, t),
                lowImpact = Mathf.Lerp(a.lowImpact, b.lowImpact, t),
                highImpact = Mathf.Lerp(a.highImpact, b.highImpact, t),
                fullness = Mathf.Lerp(a.fullness, b.fullness, t),
                sharpness = Mathf.Lerp(a.sharpness, b.sharpness, t),
                tonality = Mathf.Lerp(a.tonality, b.tonality, t),
                harmonicChange = Mathf.Lerp(a.harmonicChange, b.harmonicChange, t),
                sectionLift = Mathf.Lerp(a.sectionLift, b.sectionLift, t),
                beatDensity = Mathf.Lerp(a.beatDensity, b.beatDensity, t),
                beatConfidence = Mathf.Lerp(a.beatConfidence, b.beatConfidence, t),
                bpm = Mathf.Lerp(a.bpm, b.bpm, t),
                heavy = Mathf.Lerp(a.heavy, b.heavy, t),
                calibrationConfidence = Mathf.Lerp(a.calibrationConfidence, b.calibrationConfidence, t),
                profileSeconds = Mathf.Lerp(a.profileSeconds, b.profileSeconds, t)
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
