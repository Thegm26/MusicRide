mergeInto(LibraryManager.library, {
  $MusicRoadCapture: {
    stream: null,
    context: null,
    analyser: null,
    source: null,
    frequencyData: null,
    timeData: null,
    previousSpectrum: null,
    animationFrame: 0,
    lastAnalysisTime: 0,
    rollingEnergy: 0.05,
    rollingFlux: 0.002,
    rollingBass: 0.03,
    rollingVocal: 0.03,
    rollingTreble: 0.02,
    levelHistory: [],
    historyIndex: 0,
    windowFloor: 0.01,
    windowCeiling: 0.16,
    windowUpdateCounter: 0,
    lastAudibleTime: 0,
    resetWindowOnNextSignal: false,
    windowReadySent: false,
    lastBeatTime: 0,
    captureStartedAt: 0,
    signalDetected: false,
    silentWarningSent: false,

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

    updateSongWindow: function (level) {
      var capacity = 800;
      if (MusicRoadCapture.levelHistory.length < capacity) {
        MusicRoadCapture.levelHistory.push(level);
      } else {
        MusicRoadCapture.levelHistory[MusicRoadCapture.historyIndex] = level;
        MusicRoadCapture.historyIndex =
          (MusicRoadCapture.historyIndex + 1) % capacity;
      }

      MusicRoadCapture.windowUpdateCounter++;
      if (
        MusicRoadCapture.windowUpdateCounter < 8 ||
        MusicRoadCapture.levelHistory.length < 12
      ) {
        return;
      }
      MusicRoadCapture.windowUpdateCounter = 0;

      var sorted = MusicRoadCapture.levelHistory.slice().sort(function (a, b) {
        return a - b;
      });
      var floorIndex = Math.floor((sorted.length - 1) * 0.05);
      var ceilingIndex = Math.floor((sorted.length - 1) * 0.99);
      var targetFloor = sorted[floorIndex];
      var targetCeiling = Math.max(targetFloor + 0.028, sorted[ceilingIndex]);
      MusicRoadCapture.windowFloor =
        MusicRoadCapture.windowFloor * 0.82 + targetFloor * 0.18;
      MusicRoadCapture.windowCeiling =
        MusicRoadCapture.windowCeiling * 0.82 + targetCeiling * 0.18;
    },

    resetSongWindow: function (level) {
      MusicRoadCapture.levelHistory = [level];
      MusicRoadCapture.historyIndex = 0;
      MusicRoadCapture.windowFloor = Math.max(0.004, level * 0.35);
      MusicRoadCapture.windowCeiling = Math.max(
        MusicRoadCapture.windowFloor + 0.028,
        level * 1.65
      );
      MusicRoadCapture.windowUpdateCounter = 0;
      MusicRoadCapture.windowReadySent = false;
    },

    relativeBand: function (value, rollingValue, intensity) {
      var ratio = value / Math.max(0.006, rollingValue);
      var relative = Math.max(0, Math.min(1, (ratio - 0.68) / 1.15));
      return Math.min(0.92, relative * 0.72 + intensity * 0.28);
    },

    sendState: function (state, message) {
      SendMessage("AudioCaptureService", "OnCaptureState", state + "|" + (message || ""));
    },

    stop: function (notify) {
      if (MusicRoadCapture.animationFrame) {
        cancelAnimationFrame(MusicRoadCapture.animationFrame);
        MusicRoadCapture.animationFrame = 0;
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

      MusicRoadCapture.analyser = null;
      MusicRoadCapture.frequencyData = null;
      MusicRoadCapture.timeData = null;
      MusicRoadCapture.previousSpectrum = null;
      MusicRoadCapture.captureStartedAt = 0;
      MusicRoadCapture.signalDetected = false;
      MusicRoadCapture.silentWarningSent = false;
      MusicRoadCapture.rollingEnergy = 0.05;
      MusicRoadCapture.rollingFlux = 0.002;
      MusicRoadCapture.rollingBass = 0.03;
      MusicRoadCapture.rollingVocal = 0.03;
      MusicRoadCapture.rollingTreble = 0.02;
      MusicRoadCapture.levelHistory = [];
      MusicRoadCapture.historyIndex = 0;
      MusicRoadCapture.windowFloor = 0.01;
      MusicRoadCapture.windowCeiling = 0.16;
      MusicRoadCapture.windowUpdateCounter = 0;
      MusicRoadCapture.lastAudibleTime = 0;
      MusicRoadCapture.resetWindowOnNextSignal = false;
      MusicRoadCapture.windowReadySent = false;

      if (notify) {
        MusicRoadCapture.sendState("Ended", "Music sharing stopped. Click Connect Music to reconnect.");
      }
    },

    analyse: function (now) {
      var analyser = MusicRoadCapture.analyser;
      if (!analyser) {
        return;
      }

      MusicRoadCapture.animationFrame = requestAnimationFrame(MusicRoadCapture.analyse);
      if (now - MusicRoadCapture.lastAnalysisTime < 75) {
        return;
      }
      MusicRoadCapture.lastAnalysisTime = now;

      var spectrum = MusicRoadCapture.frequencyData;
      analyser.getByteFrequencyData(spectrum);
      var waveform = MusicRoadCapture.timeData;
      analyser.getFloatTimeDomainData(waveform);

      var timeEnergy = 0;
      for (var sampleIndex = 0; sampleIndex < waveform.length; sampleIndex++) {
        timeEnergy += waveform[sampleIndex] * waveform[sampleIndex];
      }
      var rawLevel = Math.sqrt(timeEnergy / waveform.length);

      var sampleRate = MusicRoadCapture.context.sampleRate;
      var binHz = sampleRate * 0.5 / spectrum.length;
      var bass = 0;
      var bassCount = 0;
      var mid = 0;
      var midCount = 0;
      var treble = 0;
      var trebleCount = 0;
      var vocal = 0;
      var vocalCount = 0;
      var total = 0;
      var weighted = 0;
      var flux = 0;

      for (var i = 1; i < spectrum.length; i++) {
        var value = spectrum[i] / 255;
        var hz = i * binHz;
        total += value;
        weighted += value * hz;
        flux += Math.max(0, value - MusicRoadCapture.previousSpectrum[i]);
        MusicRoadCapture.previousSpectrum[i] = value;

        if (hz < 250) {
          bass += value;
          bassCount++;
        } else if (hz < 2500) {
          mid += value;
          midCount++;
        } else if (hz < 12000) {
          treble += value;
          trebleCount++;
        }

        if (hz >= 250 && hz < 4200) {
          vocal += value;
          vocalCount++;
        }
      }

      bass = bassCount ? bass / bassCount : 0;
      mid = midCount ? mid / midCount : 0;
      treble = trebleCount ? treble / trebleCount : 0;
      vocal = vocalCount ? vocal / vocalCount : 0;
      var brightness = total > 0.001 ? Math.min(1, weighted / total / 9000) : 0;
      var fluxLevel = flux / spectrum.length;

      if (!MusicRoadCapture.signalDetected && rawLevel > 0.006) {
        MusicRoadCapture.signalDetected = true;
        MusicRoadCapture.sendState(
          "Active",
          "LIVE COMPUTER AUDIO DETECTED \u2022 calibrating this song for about 20 seconds."
        );
      } else if (
        !MusicRoadCapture.signalDetected &&
        !MusicRoadCapture.silentWarningSent &&
        now - MusicRoadCapture.captureStartedAt > 3000
      ) {
        MusicRoadCapture.silentWarningSent = true;
        MusicRoadCapture.sendState(
          "Active",
          "CONNECTED, BUT NO SOUND \u2022 enable Share system audio or share the tab playing music."
        );
      }

      var isAudible = rawLevel > 0.004;
      var songRestarted = false;
      if (isAudible) {
        if (MusicRoadCapture.resetWindowOnNextSignal) {
          MusicRoadCapture.resetSongWindow(rawLevel);
          MusicRoadCapture.resetWindowOnNextSignal = false;
          songRestarted = true;
          MusicRoadCapture.sendState(
            "Active",
            "LIVE \u2022 NEW SONG DETECTED \u2022 recalibrating for about 20 seconds."
          );
        } else {
          MusicRoadCapture.updateSongWindow(rawLevel);
        }
        MusicRoadCapture.lastAudibleTime = now;
      } else if (
        MusicRoadCapture.lastAudibleTime > 0 &&
        now - MusicRoadCapture.lastAudibleTime > 4000
      ) {
        MusicRoadCapture.resetWindowOnNextSignal = true;
      }

      var sampleCount = MusicRoadCapture.levelHistory.length;
      var analysisConfidence = Math.max(
        0,
        Math.min(1, (sampleCount - 240) / 160)
      );
      var conservativeIntensity = Math.min(
        0.48,
        rawLevel / (rawLevel + 0.16) * 0.62
      );
      var intensity = conservativeIntensity;
      if (sampleCount >= 240) {
        var windowRange = Math.max(
          0.028,
          MusicRoadCapture.windowCeiling - MusicRoadCapture.windowFloor
        );
        var windowPosition = Math.max(
          0,
          Math.min(
            1,
            (rawLevel - MusicRoadCapture.windowFloor) / windowRange
          )
        );
        var constrainedPosition = Math.pow(windowPosition, 1.7);
        var exceptionalPeak = Math.max(
          0,
          Math.min(
            1,
            (rawLevel - MusicRoadCapture.windowCeiling) /
              (windowRange * 0.4)
          )
        );
        var normalizedIntensity = Math.min(
          0.88,
          0.04 + constrainedPosition * 0.65 + exceptionalPeak * 0.18
        );
        intensity =
          conservativeIntensity * (1 - analysisConfidence) +
          normalizedIntensity * analysisConfidence;
        if (!MusicRoadCapture.windowReadySent) {
          MusicRoadCapture.windowReadySent = true;
          MusicRoadCapture.sendState(
            "Active",
            "LIVE SONG PROFILE READY \u2022 reacting from audible 60-second statistics."
          );
        }
      }

      if (songRestarted) {
        MusicRoadCapture.rollingEnergy = rawLevel;
        MusicRoadCapture.rollingBass = bass;
        MusicRoadCapture.rollingVocal = vocal;
        MusicRoadCapture.rollingTreble = treble;
        MusicRoadCapture.rollingFlux = Math.max(0.001, fluxLevel);
      } else if (isAudible) {
        MusicRoadCapture.rollingEnergy =
          MusicRoadCapture.rollingEnergy * 0.96 + rawLevel * 0.04;
        MusicRoadCapture.rollingBass =
          MusicRoadCapture.rollingBass * 0.96 + bass * 0.04;
        MusicRoadCapture.rollingVocal =
          MusicRoadCapture.rollingVocal * 0.96 + vocal * 0.04;
        MusicRoadCapture.rollingTreble =
          MusicRoadCapture.rollingTreble * 0.96 + treble * 0.04;
        MusicRoadCapture.rollingFlux =
          MusicRoadCapture.rollingFlux * 0.92 + fluxLevel * 0.08;
      }

      var vocalRatio = vocal / Math.max(0.006, MusicRoadCapture.rollingVocal);
      var vocalRise = Math.max(0, Math.min(1, (vocalRatio - 0.82) / 1.32));
      var vocalStrength = Math.min(
        0.86,
        vocalRise * 0.72 + intensity * 0.22
      ) * (0.55 + analysisConfidence * 0.45);
      var hitRatio =
        fluxLevel / Math.max(0.0015, MusicRoadCapture.rollingFlux);
      var percussion = Math.max(
        0,
        Math.min(1, (hitRatio - 1.05) / 1.65)
      );
      if (fluxLevel < MusicRoadCapture.rollingFlux * 0.9) {
        percussion = 0;
      } else {
        percussion = Math.min(0.88, percussion * 0.88 + intensity * 0.08) *
          (0.6 + analysisConfidence * 0.4);
      }
      if (!isAudible) {
        vocalStrength = 0;
        percussion = 0;
      }

      var seconds = now * 0.001;
      var beat =
        percussion > 0.55 &&
        seconds - MusicRoadCapture.lastBeatTime > 0.18
          ? percussion
          : 0;

      if (beat > 0) {
        MusicRoadCapture.lastBeatTime = seconds;
      }

      SendMessage(
        "AudioCaptureService",
        "OnAudioFeatures",
        JSON.stringify({
          timestamp: seconds,
          rms: intensity,
          intensity: intensity,
          vocal: vocalStrength,
          percussion: percussion,
          bass: MusicRoadCapture.relativeBand(
            bass,
            MusicRoadCapture.rollingBass,
            intensity
          ),
          mid: vocalStrength,
          treble: MusicRoadCapture.relativeBand(
            treble,
            MusicRoadCapture.rollingTreble,
            intensity
          ),
          brightness: Math.min(0.88, 0.08 + brightness * 0.78),
          onset: percussion,
          beat: beat,
        })
      );
    },
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
            "No audio was shared. Reconnect and enable Share audio in the browser dialog."
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
        MusicRoadCapture.analyser = MusicRoadCapture.context.createAnalyser();
        MusicRoadCapture.analyser.fftSize = 1024;
        MusicRoadCapture.analyser.smoothingTimeConstant = 0.68;
        MusicRoadCapture.frequencyData = new Uint8Array(
          MusicRoadCapture.analyser.frequencyBinCount
        );
        MusicRoadCapture.timeData = new Float32Array(
          MusicRoadCapture.analyser.fftSize
        );
        MusicRoadCapture.previousSpectrum = new Float32Array(
          MusicRoadCapture.analyser.frequencyBinCount
        );
        MusicRoadCapture.source.connect(MusicRoadCapture.analyser);
        MusicRoadCapture.captureStartedAt = performance.now();
        MusicRoadCapture.signalDetected = false;
        MusicRoadCapture.silentWarningSent = false;

        audioTracks.forEach(function (track) {
          track.addEventListener("ended", function () {
            MusicRoadCapture.stop(true);
          });
        });

        MusicRoadCapture.sendState(
          "Active",
          "COMPUTER AUDIO CONNECTED \u2022 waiting for sound..."
        );
        MusicRoadCapture.animationFrame = requestAnimationFrame(
          MusicRoadCapture.analyse
        );
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
