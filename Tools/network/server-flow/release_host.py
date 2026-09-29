"""Bounded, single-machine room pool with ready-gated release switching."""
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
CLAIMED = '[Server] Room claimed.'


class Pool:
    def __init__(self, config):
        self.config = config
        # rooms_per_release remains the fallback for already-installed configs.
        self.room_capacity = config.get('room_capacity', config.get('rooms_per_release', 1))
        self.warm_rooms = config.get('warm_rooms', self.room_capacity)
        self.rooms_per_release = self.room_capacity
        self.max_processes = config.get('max_processes', self.room_capacity * 2)
        if (type(self.room_capacity) is not int or not 1 <= self.room_capacity <= 64 or
                type(self.warm_rooms) is not int or not 1 <= self.warm_rooms <= self.room_capacity or
                type(self.max_processes) is not int or
                not self.room_capacity < self.max_processes <= 128):
            raise ValueError('Use 1..64 room_capacity, 1..room_capacity warm_rooms, and room_capacity < max_processes <= 128')
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

    @staticmethod
    def key(name, room_index):
        # Preserve the first-room identifier used by existing status consumers.
        return name if room_index == 0 else f'{name}#{room_index}'

    def start(self, name, room_index=0):
        key = self.key(name, room_index)
        if key in self.processes or time.monotonic() < self.retry_at.get(key, 0):
            return
        if len(self.processes) >= self.max_processes:
            return
        manifest = self.manifest(name)
        room = ''.join(secrets.choice('0123456789ABCDEFGHJKMNPQRSTVWXYZ') for _ in range(6))
        path = release_path(self.root, name) / 'server'
        log = self.root / 'logs' / f'{name}-{time.time_ns()}.log'
        # Stable physical slots, never shared by concurrent processes. Restarts do
        # not create another backend account for every room.
        slot = next(i for i in range(self.max_processes) if i not in {p['slot'] for p in self.processes.values()})
        home = self.root / 'state' / f'slot-{slot}'
        home.mkdir(parents=True, exist_ok=True)
        env = dict(os.environ, HOME=str(home), USERPROFILE=str(home))
        command = [str(path / manifest['executable']), '-batchmode', '-nographics', '-gameServer',
                   '-roomCode', room, '-region', self.config.get('region', 'kr'),
                   '-backendUrl', self.config.get('api_origin', 'https://j15d205.p.ssafy.io'),
                   '-job-worker-count', '1', '-logFile', str(log)]
        # 채팅 금칙어 목록과 기록 경로(S15P21D205-1027)는 내부 전용이라 nginx 에 없습니다. 게임
        # 서버가 같은 장비에서 돌므로 루프백으로 직접 붙고, 공유 키가 없으면 그 경로는 404 입니다.
        # 키가 설정에 없으면 두 인자를 아예 넘기지 않아 예전과 똑같이 뜹니다. 채팅은 그대로 돌고
        # 필터와 기록만 빠집니다.
        chat_key = self.config.get('chat_internal_key', '')
        if chat_key:
            command += ['-internalUrl', self.config.get('internal_api_origin', 'http://127.0.0.1:8080')]
            # 키는 명령줄이 아니라 환경으로 넘깁니다. 명령줄 인자는 같은 장비의 누구나 ps 로 읽습니다.
            env['D205_CHAT_KEY'] = chat_key
        process = subprocess.Popen(command, cwd=path, env=env, stdin=subprocess.DEVNULL,
                                   stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        self.processes[key] = dict(process=process, log=log, ready=False, claimed=False, log_position=0,
                                   started=time.monotonic(),
                                   room=room, slot=slot, release=name, room_index=room_index)

    @staticmethod
    def refresh_state(item):
        if not item['log'].exists():
            return
        with item['log'].open(encoding='utf-8', errors='replace') as stream:
            stream.seek(item['log_position'])
            for line in stream:
                message = line.rstrip()
                if message == READY:
                    item['ready'] = True
                # Older deployed servers do not emit CLAIMED yet. A real player join is
                # the earliest safe fallback signal and may only over-provision one room.
                if message == CLAIMED or message.startswith('[Network] Player joined:'):
                    item['claimed'] = True
            item['log_position'] = stream.tell()

    def replenish(self):
        if not self.active:
            return
        # Start one room at a time; don't import multiple Unity scenes at once.
        if any(p['release'] == self.active and not p['ready'] for p in self.processes.values()):
            return
        active = [p for p in self.processes.values() if p['release'] == self.active]
        if len(active) >= self.room_capacity or sum(not p['claimed'] for p in active) >= self.warm_rooms:
            return
        for room_index in range(self.room_capacity):
            key = self.key(self.active, room_index)
            if key not in self.processes and time.monotonic() >= self.retry_at.get(key, 0):
                self.start(self.active, room_index)
                break

    def step(self):
        for name, item in list(self.processes.items()):
            if item['process'].poll() is not None:
                del self.processes[name]
                self.retry_at[name] = time.monotonic() + 3
                continue
            self.refresh_state(item)
            if ('stop_at' in item or
                    not item['ready'] and time.monotonic() - item['started'] > self.config.get('startup_timeout', 90)):
                # A hung startup or retirement must not permanently consume a slot.
                if 'stop_at' not in item:
                    item['process'].terminate()
                    item['stop_at'] = time.monotonic()
                elif time.monotonic() - item['stop_at'] > 10:
                    item['process'].kill()

        incoming = read_json(self.root / 'request.json')
        if incoming and incoming.get('nonce') != (self.request or {}).get('nonce'):
            if self.outcome == 'pending':
                # Finish or time out the current request before accepting another.
                incoming = self.request
            else:
                self.request, self.request_at = incoming, time.monotonic()
                self.outcome, self.reason = 'pending', None
        if self.request and self.outcome == 'pending':
            name = self.request['release']
            try:
                manifest = self.manifest(name)
                # Photon uses Application.version in AppVersion. Concurrent incompatible
                # releases must not share a matchmaking partition.
                for running in {p['release'] for p in self.processes.values()} | ({self.active} if self.active else set()):
                    if running != name and self.manifest(running)['version'] == manifest['version']:
                        raise ValueError('Use a distinct build version for each concurrent release')
                # Reuse any living room on rollback, even if room zero ended.
                item = next((p for p in self.processes.values()
                             if p['release'] == name and p['ready'] and 'stop_at' not in p
                             and p['process'].poll() is None), None)
                if item is None:
                    self.start(name)
                    item = self.processes.get(name)
                if item and item['ready'] and 'stop_at' not in item and item['process'].poll() is None:
                    atomic_json(self.root / 'active.json', {'release': name})
                    self.active, self.outcome = name, 'activated'
                elif time.monotonic() - self.request_at > self.config.get('startup_timeout', 90):
                    self.outcome, self.reason = 'failed', 'Candidate did not become ready; previous release retained'
                    if item and name != self.active and not item['ready'] and 'stop_at' not in item:
                        item['process'].terminate()
                        item['stop_at'] = time.monotonic()
            except (ValueError, OSError, KeyError) as error:
                self.outcome, self.reason = 'failed', type(error).__name__ + ': ' + str(error)
        # Warm rooms no longer expire on their own. Retire only unused rooms from
        # a replaced release; claimed rooms must finish normally. Keep a pending
        # candidate alive until it can become active.
        if self.active:
            for item in self.processes.values():
                candidate = self.outcome == 'pending' and self.request['release'] == item['release']
                if item['release'] != self.active and not candidate and not item['claimed'] and 'stop_at' not in item:
                    item['process'].terminate()
                    item['stop_at'] = time.monotonic()
        self.replenish()
        atomic_json(self.root / 'status.json', {
            'active': self.active, 'request': self.request, 'outcome': self.outcome, 'reason': self.reason,
            'rooms_per_release': self.room_capacity, 'room_capacity': self.room_capacity,
            'warm_rooms': self.warm_rooms, 'max_processes': self.max_processes,
            'processes': {name: {'pid': item['process'].pid, 'ready': item['ready'], 'room': item['room'],
                                  'release': item['release'], 'room_index': item['room_index'], 'slot': item['slot'],
                                  'claimed': item['claimed'],
                                  'draining': item['release'] != self.active and
                                      not (self.outcome == 'pending' and self.request['release'] == item['release'])}
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
