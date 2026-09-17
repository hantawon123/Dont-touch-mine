import hashlib
from pathlib import Path
import re
import sys
import tempfile
import unittest
import zipfile

from package_release import package

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'network/server-flow'))
from releases import stage, validate


class DeliveryTests(unittest.TestCase):
    def test_runtime_only_paired_release_and_corruption_detection(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            client, server = root/'client', root/'server'
            revision = 'a'*40
            for name in ('Game.exe', 'UnityPlayer.dll', 'Game_Data/globalgamemanagers',
                         'MonoBleedingEdge/EmbedRuntime/mono.dll', 'Game_BackUpThisFolder_ButDontShipIt/source.cpp', '.env'):
                path = client/name
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_text('fixture')
            (client/'version.txt').write_text(revision)
            archive = package(client, root/'download', revision)
            self.assertEqual(f"Don't-Touch-Mine-Windows-{revision[:12]}.zip", archive.name)
            self.assertEqual((archive.parent/'version.txt').read_bytes(), (revision + '\n').encode())
            self.assertNotIn(b'\r', (archive.parent/'SHA256SUMS.txt').read_bytes())
            html = (archive.parent/'index.html').read_text(encoding='utf-8')
            self.assertIn(f'href="{archive.name}" download', html)
            self.assertNotIn('@@', html)
            self.assertTrue((archive.parent/'beta-test-favicon.png').is_file())
            self.assertTrue(all((archive.parent/name).is_file()
                                for name in ('banner.jpg', 'custom.jpg', 'win.jpg', 'highlight.jpg')))
            self.assertTrue(all((archive.parent/f'step-{step}{suffix}').is_file()
                                for step in range(1, 5) for suffix in ('.mp4', '-poster.jpg')))
            for reference in re.findall(r'(?:src|poster|data-src)="([^"#:]+\.(?:png|jpg|mp4))"', html):
                self.assertTrue((archive.parent/reference).is_file(), reference)
            self.assertIn('게임 다운로드</a>', html)
            self.assertIn('<section id="info">', html)
            self.assertIn(revision[:12], html)
            with zipfile.ZipFile(archive) as z:
                self.assertIn('Game_Data/globalgamemanagers', z.namelist())
                self.assertFalse(any('.env' in p or 'BackUp' in p for p in z.namelist()))
            self.assertIn(hashlib.sha256(archive.read_bytes()).hexdigest(), (archive.parent/'SHA256SUMS.txt').read_text())
            server.mkdir()
            (server/'version.txt').write_text(revision)
            (server/'GameServer.x86_64').write_text('fixture')
            stage(root/'runtime', revision, archive.parent, server, 'GameServer.x86_64')
            self.assertEqual(revision, validate(root/'runtime', revision)['version'])
            with self.assertRaises(ValueError):
                package(client, root/'mismatch', 'b'*40)
            (root/'runtime/releases'/revision/'web/version.txt').write_text('bad')
            with self.assertRaises(ValueError):
                validate(root/'runtime', revision)


if __name__ == '__main__':
    unittest.main()
