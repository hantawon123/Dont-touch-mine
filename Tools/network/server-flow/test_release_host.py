import json
import io
import http.client
from pathlib import Path
import tempfile
import threading
import time
import unittest
from unittest.mock import patch
from urllib.error import HTTPError
from urllib.request import Request, urlopen

from release_host import CLAIMED, Pool, READY, ReleaseHandler
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

    def claim(self, name):
        with self.pool.processes[name]['log'].open('a') as stream:
            stream.write(CLAIMED + '\n')
        self.pool.step()

    def test_ready_switch_preserves_old_process_and_retired_is_not_respawned(self):
        self.release('old'); self.release('new')
        self.request('old'); self.ready('old'); self.claim('old')
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
        self.request('old'); self.ready('old'); self.claim('old')
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
        self.request('one'); self.ready('one'); self.claim('one')
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

    def test_proxy_preserves_account_token_and_release_prefix(self):
        self.release('old'); self.request('old'); self.ready('old')
        server = create_server(self.root, port=0, handler=ReleaseHandler)
        server.origin = f'http://127.0.0.1:{server.server_port}'
        server.path_prefix = '/play'
        worker = threading.Thread(target=server.serve_forever); worker.start()
        try:
            connection = http.client.HTTPConnection('127.0.0.1', server.server_port)
            connection.request('GET', '/')
            response = connection.getresponse()
            self.assertEqual(response.getheader('Location'), '/play/releases/old/web/')
            response.read(); connection.close()
            with urlopen(server.origin + '/current.json') as response:
                self.assertEqual(json.load(response)['revision'], 'old')
            upstream = io.BytesIO(b'{}')
            upstream.code = 200; upstream.headers = {'Content-Type': 'application/json'}
            with patch('serve.upstream.open', return_value=upstream) as send:
                with urlopen(Request(server.origin + '/api/v1/presence', data=b'{}', method='PUT',
                                     headers={'X-Account-Token': 'synthetic-test-token'})) as response:
                    self.assertEqual(response.status, 200)
                sent = dict((k.lower(), v) for k, v in send.call_args.args[0].header_items())
                self.assertEqual(sent['x-account-token'], 'synthetic-test-token')
        finally:
            server.shutdown(); worker.join(); server.server_close()

    def test_publish_waits_for_ready_and_refuses_mixed_artifacts(self):
        from publish_release import publish
        revision = 'a' * 40
        source = self.root / 'publish-source'
        for folder in ('web', 'server'):
            (source / folder).mkdir(parents=True)
            (source / folder / 'version.txt').write_text(revision)
        (source / 'web/index.html').write_text('test')
        (source / 'server/GameServer.x86_64').write_text('test')
        def advance(_):
            self.pool.step()
            self.ready(revision)
        with patch('publish_release.time.sleep', side_effect=advance):
            publish(self.root, revision, source / 'web', source / 'server')
        self.assertEqual(self.pool.active, revision)
        (source / 'server/version.txt').write_text('different')
        with self.assertRaises(ValueError):
            publish(self.root, revision, source / 'web', source / 'server')
        self.assertEqual(self.pool.active, revision)

    def test_modified_or_overwritten_release_is_rejected(self):
        self.release('old')
        (self.root / 'releases/old/web/Build/code.js').write_text('changed')
        with self.assertRaises(ValueError):
            validate(self.root, 'old')
        source = self.root / 'source-old'
        with self.assertRaises(ValueError):
            stage(self.root, 'old', source / 'web', source / 'server', 'game')

    def test_multiple_rooms_have_isolated_identity_and_only_finished_slot_restarts(self):
        self.pool = Pool({'root': str(self.root), 'rooms_per_release': 3, 'max_processes': 6})
        self.release('one')
        with patch('release_host.subprocess.Popen', side_effect=Child) as spawn:
            self.request('one')
            self.assertEqual(len(self.pool.processes), 1)
            self.ready('one')
            self.assertEqual(len(self.pool.processes), 2)
            self.ready('one#1')
            self.ready('one#2')
            homes = [call.kwargs['env']['HOME'] for call in spawn.call_args_list]
            self.assertEqual(len(set(homes)), 3)
            self.assertEqual(len({p['room'] for p in self.pool.processes.values()}), 3)
            self.assertEqual(len({p['slot'] for p in self.pool.processes.values()}), 3)
            survivors = {k: p['process'] for k, p in self.pool.processes.items() if k != 'one#1'}
            self.pool.processes['one#1']['process'].returncode = 0
            self.pool.step()
            self.pool.retry_at['one#1'] = 0
            self.pool.step()
            self.assertEqual(len(self.pool.processes), 3)
            for key, child in survivors.items():
                self.assertIs(self.pool.processes[key]['process'], child)
                self.assertFalse(child.terminated)
            self.assertIn(spawn.call_args.kwargs['env']['HOME'], homes)

    def test_multiroom_rollout_drains_each_old_room_without_replenishing_it(self):
        self.pool = Pool({'root': str(self.root), 'rooms_per_release': 2, 'max_processes': 4})
        self.release('old'); self.release('new')
        self.request('old'); self.ready('old'); self.ready('old#1')
        self.claim('old'); self.claim('old#1')
        old = [p['process'] for p in self.pool.processes.values()]
        self.request('new')
        self.assertEqual(self.pool.active, 'old')
        self.ready('new'); self.ready('new#1')
        self.assertEqual(len(self.pool.processes), 4)
        status = read_json(self.root/'status.json')['processes']
        self.assertTrue(status['old']['draining'])
        self.assertTrue(status['old#1']['draining'])
        self.assertFalse(status['new#1']['draining'])
        for child in old:
            self.assertFalse(child.terminated)
            child.returncode = 0
        self.pool.step()
        self.assertEqual(set(self.pool.processes), {'new', 'new#1'})

    def test_multiroom_cap_preserves_games_and_rollback_reuses_surviving_room(self):
        self.pool = Pool({'root': str(self.root), 'rooms_per_release': 2, 'max_processes': 3,
                          'startup_timeout': 2})
        for name in ('old', 'new', 'third'):
            self.release(name)
        self.request('old'); self.ready('old'); self.ready('old#1')
        self.claim('old'); self.claim('old#1')
        survivor = self.pool.processes['old#1']['process']
        self.request('new'); self.ready('new')
        self.request('third'); self.pool.request_at -= 3; self.pool.step()
        self.assertEqual(self.pool.active, 'new')
        self.assertEqual(len(self.pool.processes), 3)
        self.assertFalse(survivor.terminated)
        self.pool.processes['old']['process'].returncode = 0
        # Request rollback in the same tick the slot is freed.
        self.request('old')
        self.assertEqual(self.pool.active, 'old')
        self.assertIs(self.pool.processes['old#1']['process'], survivor)
        self.assertLessEqual(len(self.pool.processes), 3)

    def test_stalled_warm_room_is_recycled_without_stopping_healthy_room(self):
        self.pool = Pool({'root': str(self.root), 'rooms_per_release': 2, 'max_processes': 4,
                          'startup_timeout': 2})
        self.release('one'); self.request('one'); self.ready('one')
        healthy = self.pool.processes['one']['process']
        stalled = self.pool.processes['one#1']
        stalled['started'] -= 3
        self.pool.step()
        self.assertTrue(stalled['process'].terminated)
        self.assertFalse(healthy.terminated)
        self.pool.step(); self.pool.retry_at['one#1'] = 0; self.pool.step()
        self.assertNotEqual(self.pool.processes['one#1']['process'].pid, stalled['process'].pid)

    def test_twenty_room_configuration_fills_exactly_twenty_slots(self):
        self.pool = Pool({'root': str(self.root), 'rooms_per_release': 20, 'max_processes': 40})
        self.release('twenty'); self.request('twenty')
        for index in range(20):
            self.ready(self.pool.key('twenty', index))
        for _ in range(5):
            self.pool.step()
        self.assertEqual(len(self.pool.processes), 20)
        self.assertEqual(len({p['slot'] for p in self.pool.processes.values()}), 20)
        self.assertTrue(all(p['ready'] for p in self.pool.processes.values()))

    def test_two_warm_rooms_expand_on_claim_up_to_twenty_room_capacity(self):
        self.pool = Pool({'root': str(self.root), 'room_capacity': 20, 'warm_rooms': 2,
                          'max_processes': 40})
        self.release('twenty'); self.request('twenty')
        for index in range(20):
            key = self.pool.key('twenty', index)
            self.ready(key)
            self.claim(key)
        for _ in range(3):
            self.pool.step()
        self.assertEqual(len(self.pool.processes), 20)
        self.assertTrue(all(p['claimed'] for p in self.pool.processes.values()))
        status = read_json(self.root/'status.json')
        self.assertEqual(status['room_capacity'], 20)
        self.assertEqual(status['warm_rooms'], 2)

    def test_healthy_warm_rooms_survive_multiple_idle_timeouts(self):
        self.pool = Pool({'root': str(self.root), 'room_capacity': 20, 'warm_rooms': 2, 'max_processes': 40})
        self.release('warm'); self.request('warm')
        self.ready('warm'); self.ready('warm#1')
        pids = {p['process'].pid for p in self.pool.processes.values()}
        for item in self.pool.processes.values():
            item['started'] -= 7200
        for _ in range(10):
            self.pool.step()
        self.assertEqual({p['process'].pid for p in self.pool.processes.values()}, pids)
        self.assertTrue(all(not p['process'].terminated for p in self.pool.processes.values()))

    def test_activation_retires_old_unused_room_but_preserves_claimed_room(self):
        self.pool = Pool({'root': str(self.root), 'room_capacity': 20, 'warm_rooms': 2, 'max_processes': 40})
        self.release('old'); self.release('new'); self.request('old')
        self.ready('old'); self.ready('old#1'); self.claim('old')
        playing = self.pool.processes['old']['process']
        unused = self.pool.processes['old#1']['process']
        self.request('new')
        self.assertFalse(unused.terminated, 'Do not retire the old pool before activation succeeds.')
        candidate = self.pool.processes['new']['process']
        self.assertFalse(candidate.terminated)
        self.ready('new')
        self.assertTrue(unused.terminated)
        self.assertFalse(playing.terminated)
        self.assertFalse(candidate.terminated)

    def test_retiring_room_is_not_reused_on_rollback_and_is_killed_after_grace(self):
        self.release('old'); self.release('new')
        self.request('old'); self.ready('old')
        old = self.pool.processes['old']
        with patch.object(old['process'], 'terminate'), patch.object(old['process'], 'kill', create=True) as kill:
            self.request('new'); self.ready('new')
            self.assertIn('stop_at', old)
            self.request('old')
            self.assertEqual(self.pool.active, 'new')
            old['stop_at'] -= 11
            self.pool.step()
            kill.assert_called_once()

    def test_chat_key_reaches_the_server_and_is_omitted_when_unset(self):
        """채팅 금칙어 목록과 기록 경로의 키 전달 (S15P21D205-1027).

        키는 환경으로 넘긴다. 명령줄 인자는 같은 장비의 누구나 ps 로 읽으므로 거기 두지 않는다.

        키가 없으면 주소도 넘기지 않는다. 빈 값을 넘기면 게임 서버가 그것을 키로 들고 404 를
        받으며 경고를 남기는데, 설정하지 않은 것과 잘못 설정한 것은 다른 상태다.
        """
        self.pool = Pool({'root': str(self.root), 'chat_internal_key': 'secret',
                          'internal_api_origin': 'http://127.0.0.1:9999'})
        self.release('keyed')
        with patch('release_host.subprocess.Popen', side_effect=Child) as spawn:
            self.request('keyed')
            command, env = spawn.call_args.args[0], spawn.call_args.kwargs['env']
        self.assertEqual(env['D205_CHAT_KEY'], 'secret')
        self.assertNotIn('secret', command)
        self.assertEqual(command[command.index('-internalUrl') + 1], 'http://127.0.0.1:9999')

        self.pool = Pool({'root': str(self.root)})
        self.release('plain')
        with patch('release_host.subprocess.Popen', side_effect=Child) as spawn:
            self.request('plain')
            command, env = spawn.call_args.args[0], spawn.call_args.kwargs['env']
        self.assertNotIn('D205_CHAT_KEY', env)
        self.assertNotIn('-internalUrl', command)

    def test_invalid_pool_limits_are_rejected_before_spawning(self):
        for rooms, cap in ((0, 2), (2, 2), (True, 2), (65, 128), (2, 129), ('2', 4)):
            with self.subTest(rooms=rooms, cap=cap), self.assertRaises(ValueError):
                Pool({'root': str(self.root), 'rooms_per_release': rooms, 'max_processes': cap})
        for capacity, warm, cap in ((20, 0, 40), (20, 21, 40), (True, 1, 2), (20, True, 40)):
            with self.subTest(capacity=capacity, warm=warm, cap=cap), self.assertRaises(ValueError):
                Pool({'root': str(self.root), 'room_capacity': capacity, 'warm_rooms': warm,
                      'max_processes': cap})



if __name__ == '__main__':
    unittest.main()
