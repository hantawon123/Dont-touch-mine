import hashlib
from pathlib import Path
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
            self.assertEqual((archive.parent/'version.txt').read_bytes(), (revision + '\n').encode())
            self.assertNotIn(b'\r', (archive.parent/'SHA256SUMS.txt').read_bytes())
            html = (archive.parent/'index.html').read_text(encoding='utf-8')
            self.assertIn(f'href="{archive.name}" download', html)
            self.assertNotIn('@@', html)
            self.assertTrue((archive.parent/'hero.png').is_file())
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
