"""Loopback-only WebGL preview with the game's existing HTTPS API as fixed upstream."""
import argparse
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.error import HTTPError
from urllib.parse import unquote, urlsplit
from urllib.request import HTTPRedirectHandler, Request, build_opener

class NoRedirect(HTTPRedirectHandler):
    def redirect_request(self, *args, **kwargs):
        return None

upstream = build_opener(NoRedirect())

class PreviewServer(ThreadingHTTPServer):
    # Fail if another preview owns this port instead of serving stale code.
    allow_reuse_address = False

class Handler(SimpleHTTPRequestHandler):
    def __init__(self, request, client_address, server):
        super().__init__(request, client_address, server, directory=str(server.root))

    def log_message(self, format, *args):
        # URLs, request bodies and credentials must not enter a shared log.
        pass

    def api(self):
        path = unquote(urlsplit(self.path).path)
        if (self.headers.get('Host') != urlsplit(self.server.origin).netloc or
                self.headers.get('Origin', self.server.origin) != self.server.origin or
                not self.path.startswith('/api/v1/') or '..' in path.split('/')):
            self.send_error(403)
            return
        try:
            size = int(self.headers.get('Content-Length', '0'))
            if not 0 <= size <= 2 * 1024 * 1024:
                self.send_error(413)
                return
            payload = self.rfile.read(size) if size else None
            headers = {key: self.headers[key] for key in
                       ('Content-Type', 'Authorization', 'Accept', 'X-User-Id', 'X-Device-Id')
                       if key in self.headers}
            request = Request(self.server.api_origin + self.path,
                              data=payload, headers=headers, method=self.command)
            try:
                response = upstream.open(request, timeout=20)
            except HTTPError as error:
                response = error
            with response:
                data = response.read(8 * 1024 * 1024 + 1)
                if len(data) > 8 * 1024 * 1024:
                    self.send_error(502)
                    return
                self.send_response(response.code)
                self.send_header('Content-Type', response.headers.get('Content-Type', 'application/json'))
                self.send_header('Content-Length', str(len(data)))
                self.end_headers()
                self.wfile.write(data)
        except Exception:
            self.send_error(502, 'Game API unavailable')

    def do_GET(self):
        if self.path.startswith('/api/'):
            self.api()
        else:
            super().do_GET()

    do_POST = do_PUT = do_PATCH = do_DELETE = api

    def guess_type(self, path):
        plain = path[:-3] if path.endswith('.gz') else path
        if plain.endswith('.wasm'):
            return 'application/wasm'
        return super().guess_type(plain)

    def end_headers(self):
        if urlsplit(self.path).path.endswith('.gz'):
            self.send_header('Content-Encoding', 'gzip')
        self.send_header('Cache-Control', 'no-store')
        super().end_headers()

def create_server(root, bind='127.0.0.1', port=4291,
                  origin='http://localhost:4291', api_origin='https://j15d205.p.ssafy.io', handler=Handler):
    parsed = urlsplit(api_origin)
    if parsed.scheme != 'https' or not parsed.netloc or parsed.path not in ('', '/') or parsed.username or parsed.password:
        raise ValueError('API upstream must be a fixed HTTPS origin without credentials')
    server = PreviewServer((bind, port), handler)
    server.root = Path(root).resolve()
    server.origin = origin.rstrip('/')
    server.api_origin = api_origin.rstrip('/')
    return server


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('root', type=Path)
    args = parser.parse_args()
    if not (args.root / 'index.html').is_file():
        raise SystemExit('Use the WebGL build directory containing index.html')
    print('http://localhost:4291', flush=True)
    create_server(args.root).serve_forever()


if __name__ == '__main__':
    main()
