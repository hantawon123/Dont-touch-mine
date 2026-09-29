"""Local-only uncompressed WebGL preview and bounded, credential-free probe event log."""
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
import argparse
import time
import json
from urllib.request import Request, urlopen

parser = argparse.ArgumentParser()
parser.add_argument('root', type=Path)
parser.add_argument('--port', type=int, default=4290)
parser.add_argument('--log', type=Path)
args = parser.parse_args()
root = args.root.resolve()

class Handler(SimpleHTTPRequestHandler):
    def __init__(self, *a, **kw):
        super().__init__(*a, directory=str(root), **kw)

    def do_POST(self):
        size = int(self.headers.get('Content-Length', '0'))
        if self.path == '/api/v1/accounts' and 0 < size <= 256:
            # Fixed upstream only; retain normal authentication and TLS validation.
            # Never log the request device ID or the response credentials.
            try:
                if self.headers.get('Origin') != f'http://127.0.0.1:{args.port}' or self.headers.get('Content-Type', '').split(';')[0] != 'application/json':
                    self.send_error(403)
                    return
                payload = self.rfile.read(size)
                device = json.loads(payload)
                if set(device) != {'deviceId'} or not isinstance(device['deviceId'], str):
                    raise ValueError('Invalid device request')
                request = Request('https://j15d205.p.ssafy.io/api/v1/accounts',
                                  data=payload, headers={'Content-Type': 'application/json'})
                with urlopen(request, timeout=15) as upstream:
                    response = upstream.read()
                self.send_response(200)
                self.send_header('Content-Type', 'application/json')
                self.send_header('Content-Length', str(len(response)))
                self.end_headers()
                self.wfile.write(response)
            except Exception:
                self.send_error(502, 'Account endpoint unavailable')
            return
        if self.path != '/probe-event' or not 0 < size <= 2048:
            self.send_error(400)
            return
        message = self.rfile.read(size).decode('utf-8')
        line = f'{time.time():.3f} {message}'
        print(line, flush=True)
        if args.log:
            with args.log.open('a', encoding='utf-8') as output:
                output.write(line + '\n')
        self.send_response(204)
        self.end_headers()

    def end_headers(self):
        self.send_header('Cache-Control', 'no-store')
        super().end_headers()

print(f'http://127.0.0.1:{args.port}', flush=True)
ThreadingHTTPServer(('127.0.0.1', args.port), Handler).serve_forever()
