# Music Calibration Console

Local browser tool for labeling Music Road's audio reactions. It combines
short-window dynamics with YAMNet sound categories so vocals, drums, and
instrument families can be inspected separately.

## First-time setup

```bash
cd Tools/MusicCalibration
npm install
mkdir -p models
curl -L 'https://tfhub.dev/google/lite-model/yamnet/classification/tflite/1?lite-format=tflite' -o models/yamnet.tflite
python3 server.py --port 8090
```

Open `http://127.0.0.1:8090`, select **Start Listening**, share the browser tab
playing music, and enable the dialog's audio-sharing checkbox.

Model files, dependencies, and captured calibration labels remain local and are
excluded from Git.
