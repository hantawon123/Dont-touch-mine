import subprocess
import unittest
from unittest.mock import patch
from changes import relevant, required


class ChangeScopeTests(unittest.TestCase):
    def test_only_known_unrelated_paths_are_skipped(self):
        for path in ('backend/Jenkinsfile', 'docs/game.md', 'README.md'):
            self.assertFalse(relevant(path))
        for path in ('Assets/UI.prefab', 'Packages/manifest.json',
                     'ProjectSettings/ProjectSettings.asset', 'Tools/webgl/build.sh',
                     '.gitattributes', 'unknown.txt'):
            self.assertTrue(relevant(path))

    @patch('changes.Path.exists', return_value=False)
    def test_missing_verified_build_requires_build(self, _):
        self.assertTrue(required())

    @patch('changes.Path.exists', return_value=True)
    @patch('changes.Path.read_text', return_value='a' * 40)
    @patch('changes.subprocess.check_output')
    def test_diff_failures_and_changes(self, diff, *_):
        diff.return_value = b'backend/a\0docs/b\0'
        self.assertFalse(required())
        diff.return_value = b'backend/a\0Assets/deleted.prefab\0'
        self.assertTrue(required())
        self.assertIn('--no-renames', diff.call_args.args[0])
        diff.side_effect = subprocess.CalledProcessError(128, 'git')
        self.assertTrue(required())


if __name__ == '__main__':
    unittest.main()
