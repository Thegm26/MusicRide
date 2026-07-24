#!/usr/bin/env python3
"""Serve the latest Music Road WebGL build with Unity Brotli headers."""

from functools import partial
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path


PROJECT_ROOT = Path(__file__).resolve().parents[1]
BUILD_ROOT = PROJECT_ROOT / "Build"
HOST = "127.0.0.1"
PORT = 8085


class UnityWebGLHandler(SimpleHTTPRequestHandler):
    def guess_type(self, path):
        if path.endswith(".wasm.unityweb"):
            return "application/wasm"
        if path.endswith(".js.unityweb"):
            return "application/javascript"
        if path.endswith(".data.unityweb"):
            return "application/octet-stream"
        return super().guess_type(path)

    def end_headers(self):
        self.send_header("Cache-Control", "no-store, max-age=0")
        if self.path.endswith(".unityweb"):
            self.send_header("Content-Encoding", "br")
        super().end_headers()


if __name__ == "__main__":
    handler = partial(UnityWebGLHandler, directory=BUILD_ROOT)
    server = ThreadingHTTPServer((HOST, PORT), handler)
    print(f"Music Road is running at http://{HOST}:{PORT}/", flush=True)
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass
    finally:
        server.server_close()
