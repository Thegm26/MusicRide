(() => {
  "use strict";

  const BUFFER_SIZE = 2048;
  const PROFILE_SECONDS = 60;
  const clamp = value => Math.max(0, Math.min(1, Number.isFinite(value) ? value : 0));
  const captureButton = document.querySelector("#capture");
  const labelControls = document.querySelector("#labelControls");
  const statusText = document.querySelector("#status");
  const signalDot = document.querySelector("#signalDot");
  const savedText = document.querySelector("#saved");
  const canvas = document.querySelector("#history");
  const ctx = canvas.getContext("2d");

  let stream = null;
  let audioContext = null;
  let sourceNode = null;
  let meydaAnalyzer = null;
  let previous = null;
  let latest = {};
  let ending = false;
  let labelCount = 0;
  let lastUiAt = 0;
  let lastPostAt = 0;
  let sectionEnergy = 0;
  let lastAudibleAt = 0;
  let resetOnNextSignal = false;
  const frames = [];
  const chartHistory = [];
  const onsetTimes = [];

  function percentile(values, amount) {
    if (!values.length) return 0;
    const sorted = values.slice().sort((a, b) => a - b);
    return sorted[Math.floor((sorted.length - 1) * amount)];
  }

  function mean(values) {
    return values.length ? values.reduce((sum, value) => sum + value, 0) / values.length : 0;
  }

  function bandMean(values, from, to) {
    if (!values?.length) return 0;
    return mean(values.slice(from, Math.min(to, values.length)));
  }

  function normalized(key, value, lowPercentile = .12, highPercentile = .94) {
    const values = frames.map(frame => frame[key]).filter(Number.isFinite);
    if (values.length < 24) return 0;
    const floor = percentile(values, lowPercentile);
    const ceiling = Math.max(floor + .000001, percentile(values, highPercentile));
    return clamp((value - floor) / (ceiling - floor));
  }

  function chromaDistance(current, prior) {
    if (!current?.length || !prior?.length) return 0;
    let dot = 0;
    let currentLength = 0;
    let priorLength = 0;
    for (let index = 0; index < Math.min(current.length, prior.length); index++) {
      dot += current[index] * prior[index];
      currentLength += current[index] * current[index];
      priorLength += prior[index] * prior[index];
    }
    return 1 - dot / Math.max(.000001, Math.sqrt(currentLength * priorLength));
  }

  function positiveSpectralFlux(current, prior) {
    if (!current?.length || !prior?.length) return 0;
    let flux = 0;
    for (let index = 0; index < Math.min(current.length, prior.length); index++) {
      flux += Math.max(0, current[index] - prior[index]);
    }
    return flux / Math.max(1, current.length);
  }

  function estimateRhythm(now, onset) {
    if (onset > .68 && (!onsetTimes.length || now - onsetTimes.at(-1) > 170)) {
      onsetTimes.push(now);
    }
    while (onsetTimes.length && now - onsetTimes[0] > 12000) onsetTimes.shift();

    const recent = onsetTimes.filter(time => now - time <= 4000);
    const density = clamp(recent.length / 9);
    if (onsetTimes.length < 4) return { bpm: 0, confidence: 0, density };

    const intervals = [];
    for (let index = 1; index < onsetTimes.length; index++) {
      let interval = onsetTimes[index] - onsetTimes[index - 1];
      while (interval < 333) interval *= 2;
      while (interval > 1000) interval /= 2;
      intervals.push(interval);
    }
    const typical = percentile(intervals, .5);
    const deviations = intervals.map(interval => Math.abs(interval - typical));
    const consistency = 1 - clamp(percentile(deviations, .5) / Math.max(1, typical) / .24);
    return {
      bpm: Math.round(60000 / Math.max(1, typical)),
      confidence: clamp(consistency * Math.min(1, intervals.length / 8)),
      density
    };
  }

  function remember(frame) {
    frames.push(frame);
    const framesPerSecond = audioContext.sampleRate / BUFFER_SIZE;
    const maximumFrames = Math.ceil(PROFILE_SECONDS * framesPerSecond);
    if (frames.length > maximumFrames) frames.shift();
  }

  function handleFeatures(features) {
    const now = performance.now();
    const rawRms = features.rms || 0;
    const audible = rawRms > .004;
    if (audible && resetOnNextSignal) {
      resetProfile();
      resetOnNextSignal = false;
      statusText.textContent = "New song detected • building a fresh profile";
    }
    if (audible) {
      lastAudibleAt = now;
    } else if (lastAudibleAt > 0 && now - lastAudibleAt > 4000) {
      resetOnNextSignal = true;
    }
    const bark = features.loudness?.specific || [];
    const perceivedLoudness = features.loudness?.total || 0;
    const lowBand = bandMean(bark, 0, 7);
    const midBand = bandMean(bark, 6, 17);
    const highBand = bandMean(bark, 16, 24);
    const flux = positiveSpectralFlux(features.amplitudeSpectrum, previous?.amplitudeSpectrum);
    const loudRise = Math.max(0, perceivedLoudness - (previous?.perceivedLoudness || perceivedLoudness));
    const lowRise = Math.max(0, lowBand - (previous?.lowBand || lowBand));
    const midRise = Math.max(0, midBand - (previous?.midBand || midBand));
    const highRise = Math.max(0, highBand - (previous?.highBand || highBand));
    const harmonicMotion = chromaDistance(features.chroma, previous?.chroma);
    const profileSeconds = frames.length * BUFFER_SIZE / Math.max(1, audioContext.sampleRate);

    let energy = 0;
    let onset = 0;
    let lowImpact = 0;
    let highImpact = 0;
    let vocalLift = 0;
    let fullness = 0;
    let tonality = 0;
    let harmonicChange = 0;
    let sectionLift = 0;
    let rhythm = { bpm: 0, confidence: 0, density: 0 };

    if (audible) {
      const decibels = 20 * Math.log10(Math.max(rawRms, .000001));
      const absoluteEnergy = clamp((decibels + 42) / 30);
      const contextualEnergy = normalized("perceivedLoudness", perceivedLoudness, .12, .97);
      const contextWeight = frames.length < 24 ? 0 : clamp((profileSeconds - 1) / 7) * .25;
      energy = absoluteEnergy * (1 - contextWeight) + contextualEnergy * contextWeight;
      const fluxRise = normalized("flux", flux, .25, .97);
      const volumeAttack = normalized("loudRise", loudRise, .35, .97);
      onset = clamp(fluxRise * .58 + volumeAttack * .42);
      lowImpact = clamp(normalized("lowRise", lowRise, .35, .97) * .7 + onset * .3);
      highImpact = clamp(normalized("highRise", highRise, .35, .97) * .7 + onset * .3);
      fullness = clamp(((features.perceptualSpread || 0) - .25) / .65);
      const flatness = clamp(features.spectralFlatness || 0);
      const chroma = features.chroma || [];
      const chromaMean = mean(chroma);
      const chromaPeak = chroma.length ? Math.max(...chroma) : 0;
      const chromaFocus = clamp((chromaPeak / Math.max(.001, chromaMean) - 1) / 3);
      tonality = clamp((1 - Math.sqrt(flatness)) * .72 + chromaFocus * .28);
      harmonicChange = normalized("harmonicMotion", harmonicMotion, .2, .95);
      vocalLift = clamp(normalized("midRise", midRise, .3, .97) * (.55 + tonality * .45));
      rhythm = estimateRhythm(now, onset);
      sectionEnergy = sectionEnergy * .965 + energy * .035;
      sectionLift = clamp(
        sectionEnergy * .48 +
        fullness * .2 +
        rhythm.density * .17 +
        harmonicChange * .15
      );
    } else {
      sectionEnergy *= .98;
    }

    const calibrationConfidence = clamp(profileSeconds / 8);
    const rawHeavy = clamp(
      Math.pow(energy, 1.25) * .48 +
      sectionLift * .22 +
      rhythm.density * .1 +
      Math.max(lowImpact, highImpact) * .12 +
      fullness * .08
    );
    const heavy = audible ? rawHeavy : 0;

    latest = {
      clientTime: Date.now(),
      audible,
      rawRms,
      perceivedLoudness,
      intensity: energy,
      energy,
      vocal: vocalLift,
      vocalLift,
      percussion: onset,
      onset,
      lowImpact,
      highImpact,
      fullness,
      sharpness: clamp(features.perceptualSharpness || 0),
      tonality,
      harmonicChange,
      sectionLift,
      beatDensity: rhythm.density,
      beatConfidence: rhythm.confidence,
      bpm: rhythm.bpm,
      flux,
      spectralCentroid: features.spectralCentroid || 0,
      spectralFlatness: features.spectralFlatness || 0,
      spectralRolloff: features.spectralRolloff || 0,
      heavy,
      calibrationConfidence,
      profileSeconds,
      sampleCount: frames.length
    };

    if (audible) {
      remember({
        perceivedLoudness, flux, loudRise, lowRise, midRise, highRise,
        harmonicMotion, energy
      });
    }
    previous = {
      perceivedLoudness,
      lowBand,
      midBand,
      highBand,
      chroma: features.chroma,
      amplitudeSpectrum: features.amplitudeSpectrum
    };

    if (now - lastUiAt > 55) {
      lastUiAt = now;
      chartHistory.push({ heavy, vocal: vocalLift, percussion: onset, intensity: energy });
      if (chartHistory.length > 240) chartHistory.shift();
      updateUi();
      drawHistory();
    }
    if (now - lastPostAt > 250) {
      lastPostAt = now;
      fetch("/api/frame", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(latest)
      }).catch(() => {});
    }
  }

  function setMeter(name, value) {
    document.querySelector(`#${name}Bar`).style.width = `${clamp(value) * 100}%`;
    document.querySelector(`#${name}Value`).textContent = Math.round(clamp(value) * 100);
  }

  function updateTags() {
    const tags = document.querySelector("#modelTags");
    tags.replaceChildren();
    const values = latest.audible
      ? [
          latest.bpm && latest.beatConfidence > .25
            ? `${latest.bpm} BPM • ${Math.round(latest.beatConfidence * 100)}% stable`
            : "Learning beat pattern",
          latest.lowImpact > latest.highImpact + .12 ? "Low-end impact dominant"
            : latest.highImpact > latest.lowImpact + .12 ? "High-frequency impact dominant"
            : "Broad-band movement",
          latest.tonality > .62 ? "Tonal / harmonic"
            : latest.spectralFlatness > .28 ? "Noisy / textured"
            : "Mixed timbre",
          `DSP context ${Math.min(60, Math.round(latest.profileSeconds))} sec`
        ]
      : ["Waiting for audible music…"];
    for (const value of values) {
      const tag = document.createElement("span");
      tag.className = "tag";
      tag.textContent = value;
      tags.append(tag);
    }
  }

  function updateUi() {
    const heavyPercent = Math.round(latest.heavy * 100);
    document.querySelector("#heavyValue").textContent = heavyPercent;
    document.querySelector("#scoreRing").style.setProperty("--score", `${heavyPercent}%`);
    setMeter("vocal", latest.vocalLift);
    setMeter("hit", latest.onset);
    setMeter("energy", latest.energy);
    for (const key of [
      "onset", "lowImpact", "highImpact", "vocalLift",
      "fullness", "tonality", "harmonicChange", "sectionLift"
    ]) setMeter(key, latest[key]);
    document.querySelector("#rawValue").textContent = latest.rawRms.toFixed(3);
    document.querySelector("#ratioValue").textContent = latest.perceivedLoudness.toFixed(2);
    document.querySelector("#fluxValue").textContent = latest.flux.toFixed(3);
    document.querySelector("#sampleValue").textContent = `${Math.round(latest.profileSeconds)} sec`;
    signalDot.classList.toggle("live", latest.audible);
    statusText.textContent = latest.audible
      ? `Listening • DSP active • road ${Math.round(latest.heavy * 100)}%`
      : "Connected • silence is excluded from the profile";
    updateTags();
  }

  function drawHistory() {
    ctx.clearRect(0, 0, canvas.width, canvas.height);
    ctx.strokeStyle = "#183044";
    ctx.lineWidth = 1;
    for (let y = 1; y < 4; y++) {
      ctx.beginPath();
      ctx.moveTo(0, y * canvas.height / 4);
      ctx.lineTo(canvas.width, y * canvas.height / 4);
      ctx.stroke();
    }
    const plots = [
      ["heavy", "#ff9b3d", 3],
      ["vocal", "#31e6ff", 2],
      ["percussion", "#e264ff", 2],
      ["intensity", "#60f09f", 1.5]
    ];
    for (const [key, color, width] of plots) {
      ctx.beginPath();
      ctx.strokeStyle = color;
      ctx.lineWidth = width;
      chartHistory.forEach((point, index) => {
        const x = index / 239 * canvas.width;
        const y = canvas.height - point[key] * (canvas.height - 8) - 4;
        index ? ctx.lineTo(x, y) : ctx.moveTo(x, y);
      });
      ctx.stroke();
    }
  }

  function resetProfile() {
    frames.length = 0;
    chartHistory.length = 0;
    onsetTimes.length = 0;
    previous = null;
    sectionEnergy = 0;
    lastAudibleAt = 0;
    resetOnNextSignal = false;
  }

  async function startCapture() {
    if (stream) {
      stopCapture();
      return;
    }
    try {
      const shared = await navigator.mediaDevices.getDisplayMedia({
        video: { frameRate: { ideal: 1, max: 3 } },
        audio: { suppressLocalAudioPlayback: false },
        systemAudio: "include",
        windowAudio: "system",
        selfBrowserSurface: "exclude"
      });
      const audioTracks = shared.getAudioTracks();
      if (!audioTracks.length) {
        shared.getTracks().forEach(track => track.stop());
        throw new Error("No audio was shared. Enable Share audio in the dialog.");
      }
      shared.getVideoTracks().forEach(track => track.stop());
      stream = new MediaStream(audioTracks);
      audioContext = new (window.AudioContext || window.webkitAudioContext)();
      await audioContext.resume();
      sourceNode = audioContext.createMediaStreamSource(stream);
      resetProfile();
      meydaAnalyzer = Meyda.createMeydaAnalyzer({
        audioContext,
        source: sourceNode,
        bufferSize: BUFFER_SIZE,
        featureExtractors: [
          "rms", "loudness", "amplitudeSpectrum", "spectralCentroid",
          "spectralFlatness", "spectralRolloff", "perceptualSpread",
          "perceptualSharpness", "chroma", "zcr"
        ],
        callback: handleFeatures
      });
      meydaAnalyzer.start();
      audioTracks.forEach(track => track.addEventListener("ended", stopCapture));
      captureButton.textContent = "STOP LISTENING";
      captureButton.classList.add("live");
      labelControls.classList.remove("disabled");
      statusText.textContent = "Connected • waiting for audible music";
      document.querySelector("#modelState").textContent = "Meyda DSP • analyzing locally";
    } catch (error) {
      statusText.textContent = error.message || "Audio capture failed";
      console.error(error);
    }
  }

  function stopCapture() {
    if (ending) return;
    ending = true;
    meydaAnalyzer?.stop();
    meydaAnalyzer = null;
    const activeStream = stream;
    stream = null;
    activeStream?.getTracks().forEach(track => track.stop());
    sourceNode?.disconnect();
    sourceNode = null;
    audioContext?.close();
    audioContext = null;
    captureButton.textContent = "START LISTENING";
    captureButton.classList.remove("live");
    labelControls.classList.add("disabled");
    signalDot.classList.remove("live");
    statusText.textContent = "Listening stopped";
    document.querySelector("#modelState").textContent = "Meyda DSP • local";
    ending = false;
  }

  async function saveLabel(label) {
    if (!stream) return;
    const response = await fetch("/api/label", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(label)
    });
    if (!response.ok) return;
    labelCount++;
    document.querySelector("#labelValue").textContent = labelCount;
    savedText.textContent = `Saved: ${label.kind} = ${label.name || label.value}`;
    setTimeout(() => { savedText.textContent = ""; }, 1800);
  }

  captureButton.addEventListener("click", startCapture);
  document.querySelectorAll("[data-heavy]").forEach(button => {
    button.addEventListener("click", () => saveLabel({
      kind: "heavy",
      value: Number(button.dataset.heavy),
      name: button.textContent.trim()
    }));
  });
  document.querySelectorAll("[data-mood]").forEach(button => {
    button.addEventListener("click", () => saveLabel({
      kind: "mood",
      value: button.dataset.mood
    }));
  });
  document.querySelectorAll("[data-instrument]").forEach(button => {
    button.addEventListener("click", () => saveLabel({
      kind: "event",
      value: button.dataset.instrument
    }));
  });
})();
