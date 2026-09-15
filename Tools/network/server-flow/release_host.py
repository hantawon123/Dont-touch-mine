"""Small test release host: one room per version, at most two game processes."""
import argparse
import json
import os
from pathlib import Path
import secrets
import signal
import subprocess
import threading
import time
from urllib.parse import unquote, urlsplit

from releases import atomic_json, read_json, release_path, validate
from serve import Handler, create_server

READY = '[Server] Ready; waiting for first room owner.'


class Pool:
    def __init__(self, config):
        self.config = config
        self.root = Path(config['root']).resolve()
        self.root.mkdir(parents=True, exist_ok=True)
        (self.root / 'logs').mkdir(exist_ok=True)
        self.processes = {}
        self.active = read_json(self.root / 'active.json', {}).get('release')
        self.request = None
        self.request_at = 0
        self.outcome = 'idle'
        self.reason = None
        self.checked = {}
        self.retry_at = {}
        self.stopping = threading.Event()

    def manifest(self, name):
        if name not in self.checked:
            self.checked[name] = validate(self.root, name)
        return self.checked[name]

    def start(self, name):
        if name in self.processes or time.monotonic() < self.retry_at.get(name, 0):
            return
        # ponytail: one current room plus one draining/candidate room;
        # use a measured fleet allocator when more concurrent rooms are needed.
        if len(self.processes) >= 2:
            return
        manifest = self.manifest(name)
        room = ''.join(secrets.choice('0123456789ABCDEFGHJKMNPQRSTVWXYZ') for _ in range(6))
        path = release_path(self.root, name) / 'server'
        log = self.root / 'logs' / f'{name}-{time.time_ns()}.log'
        # Two stable identities, never shared by concurrent processes. Restarts do
        # not create another backend account for every room.
        slot = next(i for i in range(2) if i not in {p['slot'] for p in self.processes.values()})
        home = self.root / 'state' / f'slot-{slot}'
        home.mkdir(parents=True, exist_ok=True)
        env = dict(os.environ, HOME=str(home), USERPROFILE=str(home))
        command = [str(path / manifest['executable']), '-batchmode', '-nographics', '-gameServer',
                   '-roomCode', room, '-region', self.config.get('region', 'kr'),
                   '-backendUrl', self.config.get('api_origin', 'https://j15d205.p.ssafy.io'),
                   '-job-worker-count', '1', '-logFile', str(log)]
        process = subprocess.Popen(command, cwd=path, env=env, stdin=subprocess.DEVNULL,
                                   stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        self.processes[name] = dict(process=process, log=log, ready=False, started=time.monotonic(), room=room, slot=slot)

    def step(self):
        for name, item in list(self.processes.items()):
            if item['process'].poll() is not None:
                del self.processes[name]
                self.retry_at[name] = time.monotonic() + 3
                continue
            if not item['ready'] and item['log'].exists():
                # Never print account or authentication log content into host status.
                with item['log'].open(encoding='utf-8', errors='replace') as stream:
                    item['ready'] = any(line.rstrip() == READY for line in stream)

        incoming = read_json(self.root / 'request.json')
        if incoming and incoming.get('nonce') != (self.request or {}).get('nonce'):
            if self.outcome == 'pending':
                # Finish or time out the current request before accepting another.
                incoming = self.request
            else:
                self.request, self.request_at = incoming, time.monotonic()
                self.outcome, self.reason = 'pending', None
        if self.active:
            self.start(self.active)
        if self.request and self.outcome == 'pending':
            name = self.request['release']
            try:
                manifest = self.manifest(name)
                # Photon uses Application.version in AppVersion. Concurrent incompatible
                # releases must not share a matchmaking partition.
                for running in set(self.processes) | ({self.active} if self.active else set()):
                    if running != name and self.manifest(running)['version'] == manifest['version']:
                        raise ValueError('Use a distinct build version for each concurrent release')
                self.start(name)
                item = self.processes.get(name)
                if item and item['ready'] and item['process'].poll() is None:
                    atomic_json(self.root / 'active.json', {'release': name})
                    self.active, self.outcome = name, 'activated'
                elif time.monotonic() - self.request_at > self.config.get('startup_timeout', 90):
                    self.outcome, self.reason = 'failed', 'Candidate did not become ready; previous release retained'
                    if item and name != self.active and not item['ready']:
                        item['process'].terminate()
            except (ValueError, OSError, KeyError) as error:
                self.outcome, self.reason = 'failed', type(error).__name__ + ': ' + str(error)
        atomic_json(self.root / 'status.json', {
            'active': self.active, 'request': self.request, 'outcome': self.outcome, 'reason': self.reason,
            'processes': {name: {'pid': item['process'].pid, 'ready': item['ready'], 'room': item['room'],
                                  'draining': name != self.active and self.outcome != 'pending'}
                          for name, item in self.processes.items()}})

    def run(self):
        try:
            while not self.stopping.is_set():
                try:
                    self.step()
                except (OSError, ValueError, KeyError):
                    # A bad control file must not kill existing game sessions.
                    atomic_json(self.root / 'status.json', {'active': self.active, 'outcome': 'control-error'})
                self.stopping.wait(1)
        finally:
            for item in self.processes.values():
                if item['process'].poll() is None:
                    item['process'].terminate()
            for item in self.processes.values():
                try:
                    item['process'].wait(timeout=10)
                except subprocess.TimeoutExpired:
                    item['process'].kill()
                    item['process'].wait()


class ReleaseHandler(Handler):
    def do_GET(self):
        path = unquote(urlsplit(self.path).path)
        if path == '/':
            active = read_json(self.server.root / 'active.json', {}).get('release')
            if not active:
                self.send_error(503, 'No ready release')
                return
            self.send_response(302)
            query = urlsplit(self.path).query
            self.send_header('Location', getattr(self.server, 'path_prefix', '') + '/releases/' + active + '/web/' + ('?' + query if query else ''))
            self.end_headers()
        elif path == '/current.json':
            active = read_json(self.server.root / 'active.json', {}).get('release')
            manifest = read_json(release_path(self.server.root, active) / 'release.json', {}) if active else {}
            data = json.dumps({'revision': manifest.get('version'), 'release': active}).encode()
            self.send_response(200)
            self.send_header('Content-Type', 'application/json')
            self.send_header('Content-Length', str(len(data)))
            self.end_headers()
            self.wfile.write(data)
        elif path.startswith('/api/v1/'):
            self.api()
        elif path.startswith('/releases/'):
            parts = path.split('/')
            if len(parts) < 5 or parts[3] != 'web' or '..' in parts or '\\' in path:
                self.send_error(404)
                return
            super().do_GET()
        else:
            self.send_error(404)

    def do_HEAD(self):
        self.send_error(405)

    def list_directory(self, path):
        self.send_error(404)
        return None


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('config', type=Path)
    args = parser.parse_args()
    config = read_json(args.config)
    pool = Pool(config)
    lock = (pool.root / 'host.lock').open('a+b')
    if os.name == 'posix':
        import fcntl
        fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
    server = create_server(pool.root, config.get('bind', '127.0.0.1'), config.get('port', 4291),
                           config.get('origin', 'http://localhost:4291'),
                           config.get('api_origin', 'https://j15d205.p.ssafy.io'), ReleaseHandler)
    server.path_prefix = config.get('path_prefix', '').rstrip('/')
    worker = threading.Thread(target=pool.run)
    worker.start()
    def stop(*_):
        pool.stopping.set()
        threading.Thread(target=server.shutdown).start()
    signal.signal(signal.SIGTERM, stop)
    signal.signal(signal.SIGINT, stop)
    try:
        server.serve_forever()
    finally:
        pool.stopping.set()
        worker.join()
        server.server_close()


if __name__ == '__main__':
    main()
