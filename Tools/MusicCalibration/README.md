# Music Calibration Console

Local browser tool for labeling Music Road's audio reactions. It uses Meyda
music-information retrieval features to separate momentary impacts, perceived
loudness, timbre, rhythm stability, harmonic movement, and sustained sections.

## First-time setup

```bash
cd Tools/MusicCalibration
npm install
python3 server.py --port 8090
```

Open `http://127.0.0.1:8090`, select **Start Listening**, share the browser tab
playing music, and enable the dialog's audio-sharing checkbox.

Dependencies and captured calibration labels remain local and are excluded from
Git.
