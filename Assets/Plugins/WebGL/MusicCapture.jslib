mergeInto(LibraryManager.library, {
  $MusicRoadCapture: {
    stream: null,
    context: null,
    source: null,
    analyzer: null,
    loadingAnalyzer: false,
    analyzerCallbacks: [],
    captureStartedAt: 0,
    signalDetected: false,
    silentWarningSent: false,
    lastAudibleTime: 0,
    resetOnNextSignal: false,
    frames: [],
    previous: null,
    onsetTimes: [],
    sectionEnergy: 0,
    lastBeatTime: 0,
    profileReadySent: false,
    bufferSize: 2048,
    profileSeconds: 60,

    clamp: function (value) {
      value = Number.isFinite(value) ? value : 0;
      return Math.max(0, Math.min(1, value));
    },

    mean: function (values) {
      if (!values || !values.length) {
        return 0;
      }
      var sum = 0;
      for (var i = 0; i < values.length; i++) {
        sum += values[i];
      }
      return sum / values.length;
    },

    percentile: function (values, amount) {
      if (!values.length) {
        return 0;
      }
      var sorted = values.slice().sort(function (a, b) {
        return a - b;
      });
      return sorted[Math.floor((sorted.length - 1) * amount)];
    },

    bandMean: function (values, from, to) {
      if (!values || !values.length) {
        return 0;
      }
      return MusicRoadCapture.mean(values.slice(from, Math.min(to, values.length)));
    },

    normalized: function (key, value, lowPercentile, highPercentile) {
      var values = [];
      for (var i = 0; i < MusicRoadCapture.frames.length; i++) {
        var candidate = MusicRoadCapture.frames[i][key];
        if (Number.isFinite(candidate)) {
          values.push(candidate);
        }
      }
      if (values.length < 24) {
        var observedMax = Math.max(value, 0.000001);
        for (var index = 0; index < values.length; index++) {
          observedMax = Math.max(observedMax, values[index]);
        }
        return MusicRoadCapture.clamp(value / (observedMax * 1.2));
      }
      var floor = MusicRoadCapture.percentile(values, lowPercentile);
      var ceiling = Math.max(
        floor + 0.000001,
        MusicRoadCapture.percentile(values, highPercentile)
      );
      return MusicRoadCapture.clamp((value - floor) / (ceiling - floor));
    },

    chromaDistance: function (current, prior) {
      if (!current || !prior || !current.length || !prior.length) {
        return 0;
      }
      var dot = 0;
      var currentLength = 0;
      var priorLength = 0;
      var count = Math.min(current.length, prior.length);
      for (var i = 0; i < count; i++) {
        dot += current[i] * prior[i];
        currentLength += current[i] * current[i];
        priorLength += prior[i] * prior[i];
      }
      return 1 - dot / Math.max(0.000001, Math.sqrt(currentLength * priorLength));
    },

    positiveSpectralFlux: function (current, prior) {
      if (!current || !prior || !current.length || !prior.length) {
        return 0;
      }
      var flux = 0;
      var count = Math.min(current.length, prior.length);
      for (var i = 0; i < count; i++) {
        flux += Math.max(0, current[i] - prior[i]);
      }
      return flux / Math.max(1, count);
    },

    estimateRhythm: function (now, onset) {
      var triggered =
        onset > 0.68 &&
        (!MusicRoadCapture.onsetTimes.length ||
          now - MusicRoadCapture.onsetTimes[MusicRoadCapture.onsetTimes.length - 1] > 170);
      if (triggered) {
        MusicRoadCapture.onsetTimes.push(now);
      }
      while (
        MusicRoadCapture.onsetTimes.length &&
        now - MusicRoadCapture.onsetTimes[0] > 12000
      ) {
        MusicRoadCapture.onsetTimes.shift();
      }

      var recentCount = 0;
      for (var i = 0; i < MusicRoadCapture.onsetTimes.length; i++) {
        if (now - MusicRoadCapture.onsetTimes[i] <= 4000) {
          recentCount++;
        }
      }
      var density = MusicRoadCapture.clamp(recentCount / 9);
      if (MusicRoadCapture.onsetTimes.length < 4) {
        return { bpm: 0, confidence: 0, density: density, triggered: triggered };
      }

      var intervals = [];
      for (var index = 1; index < MusicRoadCapture.onsetTimes.length; index++) {
        var interval =
          MusicRoadCapture.onsetTimes[index] -
          MusicRoadCapture.onsetTimes[index - 1];
        while (interval < 333) {
          interval *= 2;
        }
        while (interval > 1000) {
          interval /= 2;
        }
        intervals.push(interval);
      }
      var typical = MusicRoadCapture.percentile(intervals, 0.5);
      var deviations = intervals.map(function (interval) {
        return Math.abs(interval - typical);
      });
      var consistency =
        1 -
        MusicRoadCapture.clamp(
          MusicRoadCapture.percentile(deviations, 0.5) /
            Math.max(1, typical) /
            0.24
        );
      return {
        bpm: Math.round(60000 / Math.max(1, typical)),
        confidence: MusicRoadCapture.clamp(
          consistency * Math.min(1, intervals.length / 8)
        ),
        density: density,
        triggered: triggered,
      };
    },

    remember: function (frame) {
      MusicRoadCapture.frames.push(frame);
      var framesPerSecond =
        MusicRoadCapture.context.sampleRate / MusicRoadCapture.bufferSize;
      var capacity = Math.ceil(MusicRoadCapture.profileSeconds * framesPerSecond);
      if (MusicRoadCapture.frames.length > capacity) {
        MusicRoadCapture.frames.shift();
      }
    },

    resetProfile: function () {
      MusicRoadCapture.frames = [];
      MusicRoadCapture.previous = null;
      MusicRoadCapture.onsetTimes = [];
      MusicRoadCapture.sectionEnergy = 0;
      MusicRoadCapture.lastBeatTime = 0;
      MusicRoadCapture.profileReadySent = false;
    },

    onFeatures: function (features) {
      if (!MusicRoadCapture.context) {
        return;
      }

      var now = performance.now();
      var rawLevel = features.rms || 0;
      var audible = rawLevel > 0.004;
      if (audible && MusicRoadCapture.resetOnNextSignal) {
        MusicRoadCapture.resetProfile();
        MusicRoadCapture.resetOnNextSignal = false;
        MusicRoadCapture.sendState(
          "Active",
          "LIVE \u2022 NEW SONG DETECTED \u2022 building a fresh 60-second profile."
        );
      }
      if (audible) {
        MusicRoadCapture.lastAudibleTime = now;
      } else if (
        MusicRoadCapture.lastAudibleTime > 0 &&
        now - MusicRoadCapture.lastAudibleTime > 4000
      ) {
        MusicRoadCapture.resetOnNextSignal = true;
      }

      if (!MusicRoadCapture.signalDetected && audible) {
        MusicRoadCapture.signalDetected = true;
        MusicRoadCapture.sendState(
          "Active",
          "LIVE COMPUTER AUDIO \u2022 analyzing rhythm, timbre, impacts, and sections."
        );
      } else if (
        !MusicRoadCapture.signalDetected &&
        !MusicRoadCapture.silentWarningSent &&
        now - MusicRoadCapture.captureStartedAt > 3000
      ) {
        MusicRoadCapture.silentWarningSent = true;
        MusicRoadCapture.sendState(
          "Active",
          "CONNECTED, BUT NO SOUND \u2022 enable Share system audio or share the music tab."
        );
      }

      var bark = features.loudness ? features.loudness.specific : [];
      var perceivedLoudness = features.loudness ? features.loudness.total : 0;
      var lowBand = MusicRoadCapture.bandMean(bark, 0, 7);
      var midBand = MusicRoadCapture.bandMean(bark, 6, 17);
      var highBand = MusicRoadCapture.bandMean(bark, 16, 24);
      var prior = MusicRoadCapture.previous;
      var flux = MusicRoadCapture.positiveSpectralFlux(
        features.amplitudeSpectrum,
        prior ? prior.amplitudeSpectrum : null
      );
      var loudRise = Math.max(
        0,
        perceivedLoudness - (prior ? prior.perceivedLoudness : perceivedLoudness)
      );
      var lowRise = Math.max(0, lowBand - (prior ? prior.lowBand : lowBand));
      var midRise = Math.max(0, midBand - (prior ? prior.midBand : midBand));
      var highRise = Math.max(0, highBand - (prior ? prior.highBand : highBand));
      var harmonicMotion = MusicRoadCapture.chromaDistance(
        features.chroma,
        prior ? prior.chroma : null
      );

      var energy = 0;
      var onset = 0;
      var lowImpact = 0;
      var highImpact = 0;
      var vocalLift = 0;
      var fullness = 0;
      var tonality = 0;
      var harmonicChange = 0;
      var sectionLift = 0;
      var rhythm = { bpm: 0, confidence: 0, density: 0, triggered: false };

      if (audible) {
        energy = MusicRoadCapture.normalized(
          "perceivedLoudness",
          perceivedLoudness,
          0.12,
          0.94
        );
        var fluxRise = MusicRoadCapture.normalized("flux", flux, 0.25, 0.97);
        var volumeAttack = MusicRoadCapture.normalized(
          "loudRise",
          loudRise,
          0.35,
          0.97
        );
        onset = MusicRoadCapture.clamp(fluxRise * 0.58 + volumeAttack * 0.42);
        lowImpact = MusicRoadCapture.clamp(
          MusicRoadCapture.normalized("lowRise", lowRise, 0.35, 0.97) * 0.7 +
            onset * 0.3
        );
        highImpact = MusicRoadCapture.clamp(
          MusicRoadCapture.normalized("highRise", highRise, 0.35, 0.97) * 0.7 +
            onset * 0.3
        );
        fullness = MusicRoadCapture.clamp(features.perceptualSpread || 0);
        var flatness = MusicRoadCapture.clamp(features.spectralFlatness || 0);
        var chroma = features.chroma || [];
        var chromaMean = MusicRoadCapture.mean(chroma);
        var chromaPeak = 0;
        for (var chromaIndex = 0; chromaIndex < chroma.length; chromaIndex++) {
          chromaPeak = Math.max(chromaPeak, chroma[chromaIndex]);
        }
        var chromaFocus = MusicRoadCapture.clamp(
          (chromaPeak / Math.max(0.001, chromaMean) - 1) / 3
        );
        tonality = MusicRoadCapture.clamp(
          (1 - Math.sqrt(flatness)) * 0.72 + chromaFocus * 0.28
        );
        harmonicChange = MusicRoadCapture.normalized(
          "harmonicMotion",
          harmonicMotion,
          0.2,
          0.95
        );
        vocalLift = MusicRoadCapture.clamp(
          MusicRoadCapture.normalized("midRise", midRise, 0.3, 0.97) *
            (0.55 + tonality * 0.45)
        );
        rhythm = MusicRoadCapture.estimateRhythm(now, onset);
        MusicRoadCapture.sectionEnergy =
          MusicRoadCapture.sectionEnergy * 0.965 + energy * 0.035;
        sectionLift = MusicRoadCapture.clamp(
          MusicRoadCapture.sectionEnergy * 0.48 +
            fullness * 0.2 +
            rhythm.density * 0.17 +
            harmonicChange * 0.15
        );
      } else {
        MusicRoadCapture.sectionEnergy *= 0.98;
      }

      var profileDuration =
        (MusicRoadCapture.frames.length * MusicRoadCapture.bufferSize) /
        Math.max(1, MusicRoadCapture.context.sampleRate);
      var calibrationConfidence = MusicRoadCapture.clamp(
        (profileDuration - 8) / 52
      );
      var rawHeavy = MusicRoadCapture.clamp(
        sectionLift * 0.5 +
          onset * 0.2 +
          vocalLift * 0.14 +
          Math.max(lowImpact, highImpact) * 0.1 +
          harmonicChange * 0.06
      );
      var heavy = audible
        ? Math.min(0.55 + calibrationConfidence * 0.45, rawHeavy)
        : 0;
      var centroidHz =
        ((features.spectralCentroid || 0) *
          MusicRoadCapture.context.sampleRate) /
        MusicRoadCapture.bufferSize;
      var brightness = MusicRoadCapture.clamp(centroidHz / 8000);
      var beat =
        rhythm.triggered && now - MusicRoadCapture.lastBeatTime > 170
          ? onset
          : 0;
      if (beat > 0) {
        MusicRoadCapture.lastBeatTime = now;
      }

      if (audible) {
        MusicRoadCapture.remember({
          perceivedLoudness: perceivedLoudness,
          flux: flux,
          loudRise: loudRise,
          lowRise: lowRise,
          midRise: midRise,
          highRise: highRise,
          harmonicMotion: harmonicMotion,
          energy: energy,
        });
      }
      MusicRoadCapture.previous = {
        perceivedLoudness: perceivedLoudness,
        lowBand: lowBand,
        midBand: midBand,
        highBand: highBand,
        chroma: features.chroma,
        amplitudeSpectrum: features.amplitudeSpectrum,
      };

      if (
        !MusicRoadCapture.profileReadySent &&
        profileDuration >= MusicRoadCapture.profileSeconds
      ) {
        MusicRoadCapture.profileReadySent = true;
        MusicRoadCapture.sendState(
          "Active",
          "LIVE SONG PROFILE READY \u2022 reactions use rolling 60-second statistics."
        );
      }

      SendMessage(
        "AudioCaptureService",
        "OnAudioFeatures",
        JSON.stringify({
          timestamp: now * 0.001,
          rms: audible ? energy : 0,
          rawLevel: rawLevel,
          perceivedLoudness: perceivedLoudness,
          intensity: energy,
          energy: energy,
          vocal: vocalLift,
          vocalLift: vocalLift,
          percussion: onset,
          bass: lowImpact,
          mid: vocalLift,
          treble: highImpact,
          brightness: brightness,
          onset: onset,
          beat: beat,
          lowImpact: lowImpact,
          highImpact: highImpact,
          fullness: fullness,
          sharpness: MusicRoadCapture.clamp(features.perceptualSharpness || 0),
          tonality: tonality,
          harmonicChange: harmonicChange,
          sectionLift: sectionLift,
          beatDensity: rhythm.density,
          beatConfidence: rhythm.confidence,
          bpm: rhythm.bpm,
          heavy: heavy,
          calibrationConfidence: calibrationConfidence,
          profileSeconds: profileDuration,
        })
      );
    },

    focusGame: function () {
      try {
        window.focus();
        var canvas = document.querySelector("#unity-canvas");
        if (canvas) {
          canvas.tabIndex = 0;
          canvas.focus({ preventScroll: true });
        }
      } catch (_) {}
    },

    sendState: function (state, message) {
      SendMessage(
        "AudioCaptureService",
        "OnCaptureState",
        state + "|" + (message || "")
      );
    },

    loadAnalyzer: function (callback) {
      if (window.Meyda) {
        callback(true);
        return;
      }
      MusicRoadCapture.analyzerCallbacks.push(callback);
      if (MusicRoadCapture.loadingAnalyzer) {
        return;
      }
      MusicRoadCapture.loadingAnalyzer = true;
      var script = document.createElement("script");
      var base = Module.streamingAssetsUrl || "StreamingAssets";
      script.src = base + "/MusicRoad/meyda.min.js";
      script.onload = function () {
        MusicRoadCapture.loadingAnalyzer = false;
        var callbacks = MusicRoadCapture.analyzerCallbacks.slice();
        MusicRoadCapture.analyzerCallbacks = [];
        for (var i = 0; i < callbacks.length; i++) {
          callbacks[i](!!window.Meyda);
        }
      };
      script.onerror = function () {
        MusicRoadCapture.loadingAnalyzer = false;
        var callbacks = MusicRoadCapture.analyzerCallbacks.slice();
        MusicRoadCapture.analyzerCallbacks = [];
        for (var i = 0; i < callbacks.length; i++) {
          callbacks[i](false);
        }
      };
      document.head.appendChild(script);
    },

    startAnalyzer: function () {
      if (!window.Meyda || !MusicRoadCapture.context || !MusicRoadCapture.source) {
        MusicRoadCapture.sendState(
          "Unsupported",
          "The local music analyzer could not be loaded."
        );
        return;
      }
      MusicRoadCapture.resetProfile();
      MusicRoadCapture.analyzer = Meyda.createMeydaAnalyzer({
        audioContext: MusicRoadCapture.context,
        source: MusicRoadCapture.source,
        bufferSize: MusicRoadCapture.bufferSize,
        featureExtractors: [
          "rms",
          "loudness",
          "amplitudeSpectrum",
          "spectralCentroid",
          "spectralFlatness",
          "spectralRolloff",
          "perceptualSpread",
          "perceptualSharpness",
          "chroma",
          "zcr",
        ],
        callback: MusicRoadCapture.onFeatures,
      });
      MusicRoadCapture.analyzer.start();
      MusicRoadCapture.captureStartedAt = performance.now();
      MusicRoadCapture.signalDetected = false;
      MusicRoadCapture.silentWarningSent = false;
      MusicRoadCapture.sendState(
        "Active",
        "COMPUTER AUDIO CONNECTED \u2022 local music analyzer is ready."
      );
    },

    stop: function (notify) {
      if (MusicRoadCapture.analyzer) {
        MusicRoadCapture.analyzer.stop();
        MusicRoadCapture.analyzer = null;
      }
      if (MusicRoadCapture.stream) {
        MusicRoadCapture.stream.getTracks().forEach(function (track) {
          track.stop();
        });
        MusicRoadCapture.stream = null;
      }
      if (MusicRoadCapture.source) {
        try {
          MusicRoadCapture.source.disconnect();
        } catch (_) {}
        MusicRoadCapture.source = null;
      }
      if (MusicRoadCapture.context) {
        MusicRoadCapture.context.close();
        MusicRoadCapture.context = null;
      }
      MusicRoadCapture.resetProfile();
      MusicRoadCapture.captureStartedAt = 0;
      MusicRoadCapture.signalDetected = false;
      MusicRoadCapture.silentWarningSent = false;
      MusicRoadCapture.lastAudibleTime = 0;
      MusicRoadCapture.resetOnNextSignal = false;
      if (notify) {
        MusicRoadCapture.sendState(
          "Ended",
          "Music sharing stopped. Click Connect Music to reconnect."
        );
      }
    },
  },

  MusicRoad_PrepareCapture__deps: ["$MusicRoadCapture"],
  MusicRoad_PrepareCapture: function () {
    MusicRoadCapture.loadAnalyzer(function () {});
  },

  MusicRoad_StartCapture__deps: ["$MusicRoadCapture"],
  MusicRoad_StartCapture: function () {
    if (!navigator.mediaDevices || !navigator.mediaDevices.getDisplayMedia) {
      MusicRoadCapture.sendState(
        "Unsupported",
        "This browser cannot capture shared audio. Use current Chrome or Edge on desktop."
      );
      return;
    }

    MusicRoadCapture.stop(false);
    MusicRoadCapture.loadAnalyzer(function (ready) {
      if (
        ready &&
        MusicRoadCapture.context &&
        MusicRoadCapture.source &&
        !MusicRoadCapture.analyzer
      ) {
        MusicRoadCapture.startAnalyzer();
      }
    });

    var captureOptions = {
      video: { frameRate: { ideal: 1, max: 5 } },
      audio: { suppressLocalAudioPlayback: false },
      systemAudio: "include",
      windowAudio: "system",
      selfBrowserSurface: "exclude",
    };
    var focusController = null;
    if (typeof CaptureController !== "undefined") {
      try {
        focusController = new CaptureController();
        captureOptions.controller = focusController;
      } catch (_) {}
    }

    navigator.mediaDevices
      .getDisplayMedia(captureOptions)
      .then(function (stream) {
        if (focusController) {
          try {
            focusController.setFocusBehavior("no-focus-change");
          } catch (_) {}
        }
        MusicRoadCapture.focusGame();
        setTimeout(MusicRoadCapture.focusGame, 100);
        setTimeout(MusicRoadCapture.focusGame, 500);

        var audioTracks = stream.getAudioTracks();
        if (!audioTracks.length) {
          stream.getTracks().forEach(function (track) {
            track.stop();
          });
          MusicRoadCapture.sendState(
            "NoAudio",
            "No audio was shared. Reconnect and enable Share audio."
          );
          return;
        }

        stream.getVideoTracks().forEach(function (track) {
          track.stop();
        });
        MusicRoadCapture.stream = new MediaStream(audioTracks);
        var AudioContextClass = window.AudioContext || window.webkitAudioContext;
        MusicRoadCapture.context = new AudioContextClass();
        MusicRoadCapture.context.resume().catch(function () {});
        MusicRoadCapture.source =
          MusicRoadCapture.context.createMediaStreamSource(MusicRoadCapture.stream);

        audioTracks.forEach(function (track) {
          track.addEventListener("ended", function () {
            MusicRoadCapture.stop(true);
          });
        });

        if (window.Meyda) {
          MusicRoadCapture.startAnalyzer();
        } else {
          MusicRoadCapture.sendState(
            "Requesting",
            "Audio connected \u2022 loading the local analyzer..."
          );
        }
      })
      .catch(function (error) {
        var message =
          error && error.name === "NotAllowedError"
            ? "Screen/audio sharing was cancelled or denied."
            : "Could not capture computer audio: " +
              (error && error.message ? error.message : "unknown browser error");
        MusicRoadCapture.sendState("Denied", message);
      });
  },

  MusicRoad_StopCapture__deps: ["$MusicRoadCapture"],
  MusicRoad_StopCapture: function () {
    MusicRoadCapture.stop(false);
  },
});
