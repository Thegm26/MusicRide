#!/usr/bin/env python3
import argparse
import json
import threading
import time
from collections import deque
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import urlparse


ROOT = Path(__file__).resolve().parent
DATA_FILE = ROOT / "calibration-data.jsonl"
LOCK = threading.Lock()
FRAMES = deque(maxlen=2400)
LABELS = deque(maxlen=40)
LATEST = {}


def average_frames(seconds):
    cutoff = time.time() - seconds
    recent = [frame for frame in FRAMES if frame.get("serverTime", 0) >= cutoff]
    if not recent:
        return {}
    numeric_keys = (
        "rawRms",
        "intensity",
        "vocal",
        "vocalRaw",
        "vocalRatio",
        "percussion",
        "flux",
        "bass",
        "treble",
        "brightness",
        "heavy",
        "voiceMl",
        "drumsMl",
        "bassMl",
        "guitarMl",
        "pianoMl",
        "stringsMl",
        "synthMl",
        "brassMl",
    )
    return {
        key: round(sum(float(frame.get(key, 0)) for frame in recent) / len(recent), 5)
        for key in numeric_keys
    }


class CalibrationHandler(SimpleHTTPRequestHandler):
    def __init__(self, *args, **kwargs):
        super().__init__(*args, directory=str(ROOT), **kwargs)

    def send_json(self, value, status=200):
        payload = json.dumps(value).encode("utf-8")
        self.send_response(status)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(payload)))
        self.send_header("Cache-Control", "no-store")
        self.end_headers()
        self.wfile.write(payload)

    def do_GET(self):
        if urlparse(self.path).path == "/api/status":
            with LOCK:
                self.send_json(
                    {
                        "listening": bool(LATEST),
                        "frameCount": len(FRAMES),
                        "latest": LATEST,
                        "recent3s": average_frames(3),
                        "labels": list(LABELS)[-12:],
                    }
                )
            return
        super().do_GET()

    def do_POST(self):
        global LATEST
        path = urlparse(self.path).path
        try:
            size = int(self.headers.get("Content-Length", "0"))
            value = json.loads(self.rfile.read(size) or b"{}")
        except (ValueError, json.JSONDecodeError):
            self.send_json({"error": "Invalid JSON"}, 400)
            return

        if path == "/api/frame":
            value["serverTime"] = time.time()
            with LOCK:
                LATEST = value
                FRAMES.append(value)
            self.send_json({"ok": True})
            return

        if path == "/api/label":
            with LOCK:
                record = {
                    "type": "calibration-label",
                    "savedAt": time.strftime("%Y-%m-%dT%H:%M:%S%z"),
                    "serverTime": time.time(),
                    "label": value,
                    "instant": dict(LATEST),
                    "average3s": average_frames(3),
                    "average8s": average_frames(8),
                }
                LABELS.append(record)
                with DATA_FILE.open("a", encoding="utf-8") as output:
                    output.write(json.dumps(record, separators=(",", ":")) + "\n")
            self.send_json({"ok": True, "record": record})
            return

        self.send_json({"error": "Unknown endpoint"}, 404)

    def log_message(self, message, *args):
        if "/api/frame" not in self.path:
            super().log_message(message, *args)


def main():
    parser = argparse.ArgumentParser(description="Music Road audio calibration console")
    parser.add_argument("--port", type=int, default=8090)
    args = parser.parse_args()
    server = ThreadingHTTPServer(("127.0.0.1", args.port), CalibrationHandler)
    print(f"Music calibration console: http://127.0.0.1:{args.port}", flush=True)
    server.serve_forever()


if __name__ == "__main__":
    main()
