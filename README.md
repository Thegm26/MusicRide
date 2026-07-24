# Music Road

A Unity 6 WebGL prototype where a toy car drives along an endless procedural road shaped by music playing on the computer.

## Run in the Editor

1. Open the project with Unity `6000.4.8f1`.
2. Open `Assets/Scenes/Main.unity`.
3. Enter Play Mode. The Editor uses a synthetic music signal so every system remains testable.
4. Drive with WASD or the arrow keys. Hold Shift for nitro, press Space to jump, and press R to reset.

## Connect computer audio

Build and serve the WebGL player over HTTPS or localhost. In current Chrome or Edge:

1. Start music on the computer.
2. Click **Capture Computer Audio**.
3. In the browser permission dialog, select **Entire Screen** and enable **Share system audio** when that option is available.
4. If the browser/OS does not offer system audio, select the browser tab playing music and enable **Share tab audio**.
5. Confirm the in-game meter says **INPUT: LIVE COMPUTER AUDIO** and its bars move.

A desktop website cannot open the speaker/output device through a simple microphone-style permission. Browser security requires the screen-share picker for computer audio. Full-system capture is platform-dependent; tab audio works across the most desktop configurations. The game never uploads or records captured audio; analysis happens locally in the browser.

## Build

Use **Music Road > Build WebGL** in Unity, or:

```sh
/home/gm26/Unity/Hub/Editor/6000.4.8f1/Editor/Unity \
  -batchmode -nographics -quit \
  -projectPath /home/gm26/repos/MusicRoad \
  -executeMethod MusicRoad.Editor.MusicRoadProjectSetup.BuildWebGL \
  -logFile -
```

The build is written to `Build`.

Do not open `index.html` directly. Start the included local server:

```sh
python3 Tools/serve_webgl.py
```

Then open `http://127.0.0.1:8085/`. The server automatically supplies the
Brotli headers required by Unity WebGL.

## Asset replacement points

The prototype intentionally creates placeholder objects at runtime. Replace the generated car, roadside props, particle material, sky presentation, and road materials after importing final art. Keep `ArcadeCarController` on the player root and preserve its Rigidbody and BoxCollider.
