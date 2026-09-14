import json
from pathlib import Path
import tempfile
import threading
import time
import unittest
from unittest.mock import patch
from urllib.error import HTTPError
from urllib.request import Request, urlopen

from release_host import Pool, READY, ReleaseHandler
from releases import atomic_json, read_json, stage, validate
from serve import create_server
from sync_editor import main as sync_editor


class Child:
    next_pid = 100
    def __init__(self, *args, **kwargs):
        Child.next_pid += 1
        self.pid, self.returncode, self.terminated = Child.next_pid, None, False
    def poll(self):
        return self.returncode
    def terminate(self):
        self.returncode, self.terminated = 0, True


class ReleaseTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        self.pool = Pool({'root': str(self.root), 'startup_timeout': 2})
        self.spawn = patch('release_host.subprocess.Popen', Child)
        self.spawn.start()

    def tearDown(self):
        self.spawn.stop()
        self.temp.cleanup()

    def release(self, name, version=None):
        source = self.root / ('source-' + name)
        for folder in ('web', 'server'):
            (source / folder).mkdir(parents=True)
            (source / folder / 'version.txt').write_text(version or name)
        (source / 'web/index.html').write_text('<script src="Build/code.js"></script>')
        (source / 'web/Build').mkdir()
        (source / 'web/Build/code.js').write_text(name)
        (source / 'server/game').write_text('test fixture')
        stage(self.root, name, source / 'web', source / 'server', 'game')

    def request(self, name):
        atomic_json(self.root / 'request.json', {'release': name, 'nonce': str(time.time_ns())})
        self.pool.step()

    def ready(self, name):
        self.pool.processes[name]['log'].write_text(READY + '\n')
        self.pool.step()

    def test_ready_switch_preserves_old_process_and_retired_is_not_respawned(self):
        self.release('old'); self.release('new')
        self.request('old'); self.ready('old')
        old = self.pool.processes['old']['process']
        self.request('new')
        self.assertEqual(read_json(self.root / 'active.json')['release'], 'old')
        self.ready('new')
        self.assertEqual(self.pool.active, 'new')
        self.assertFalse(old.terminated)
        self.assertEqual(len(self.pool.processes), 2)
        old.returncode = 0
        self.pool.step()
        self.assertNotIn('old', self.pool.processes)
        current = self.pool.processes['new']['process']
        current.returncode = 0
        self.pool.step()
        self.pool.retry_at['new'] = 0
        self.pool.step()
        self.assertNotEqual(self.pool.processes['new']['process'].pid, current.pid)

    def test_failed_candidate_and_version_collision_keep_previous_route(self):
        self.release('old'); self.release('bad'); self.release('collision', 'old')
        self.request('old'); self.ready('old')
        old = self.pool.processes['old']['process']
        self.request('collision')
        self.assertEqual(self.pool.outcome, 'failed')
        self.assertNotIn('collision', self.pool.processes)
        self.request('bad')
        self.pool.request_at -= 3
        self.pool.step()
        self.assertEqual(self.pool.outcome, 'failed')
        self.assertEqual(self.pool.active, 'old')
        self.assertFalse(old.terminated)

    def test_rollback_reuses_live_old_server_and_cap_does_not_kill_games(self):
        for name in ('one', 'two', 'three'):
            self.release(name)
        self.request('one'); self.ready('one')
        old = self.pool.processes['one']['process']
        self.request('two'); self.ready('two')
        self.request('three')
        self.assertEqual(len(self.pool.processes), 2)
        self.pool.request_at -= 3
        self.pool.step()
        self.assertEqual(self.pool.active, 'two')
        self.assertFalse(old.terminated)
        self.request('one')
        self.assertEqual(self.pool.active, 'one')
        self.assertEqual(self.pool.processes['one']['process'].pid, old.pid)

    def test_pinned_assets_survive_switch_and_private_files_are_not_served(self):
        self.release('old'); self.release('new')
        self.request('old'); self.ready('old')
        server = create_server(self.root, port=0, handler=ReleaseHandler)
        origin = f'http://127.0.0.1:{server.server_port}'
        server.origin = origin
        worker = threading.Thread(target=server.serve_forever)
        worker.start()
        try:
            with urlopen(origin + '/') as response:
                self.assertIn('/releases/old/web/', response.url)
            self.request('new'); self.ready('new')
            with urlopen(origin + '/') as response:
                self.assertIn('/releases/new/web/', response.url)
            project = self.root / 'editor-project'
            (project / 'ProjectSettings').mkdir(parents=True)
            (project / 'ProjectSettings/ProjectVersion.txt').write_text('test')
            with patch('sys.argv', ['sync_editor.py', str(project), '--origin', origin]):
                sync_editor()
            self.assertEqual((project / 'UserSettings/ServerFlowVersion.txt').read_text().strip(), 'new')
            with urlopen(origin + '/releases/old/web/Build/code.js') as response:
                self.assertEqual(response.read(), b'old')
            for path in ('/request.json', '/releases/old/server/game', '/releases/old/web/%2e%2e/server/game'):
                with self.assertRaises(HTTPError) as error:
                    urlopen(origin + path)
                self.assertEqual(error.exception.code, 404)
                error.exception.close()
            with self.assertRaises(HTTPError) as error:
                urlopen(Request(origin + '/api/v1/accounts', data=b'{}', headers={'Origin': 'https://other.invalid'}))
            self.assertEqual(error.exception.code, 403)
            error.exception.close()
        finally:
            server.shutdown(); worker.join(); server.server_close()

    def test_modified_or_overwritten_release_is_rejected(self):
        self.release('old')
        (self.root / 'releases/old/web/Build/code.js').write_text('changed')
        with self.assertRaises(ValueError):
            validate(self.root, 'old')
        source = self.root / 'source-old'
        with self.assertRaises(ValueError):
            stage(self.root, 'old', source / 'web', source / 'server', 'game')


if __name__ == '__main__':
    unittest.main()
