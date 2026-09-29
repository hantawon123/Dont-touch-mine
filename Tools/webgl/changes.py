"""Skip only known unrelated changes; missing history always requires a build."""
from pathlib import Path
import subprocess


def relevant(path):
    return not (path.startswith(('backend/', 'docs/', 'source/'))
                or path in ('README.md', 'CONTRIBUTING.md'))


def required():
    marker = Path('Library/WebGLCiCache/verified-revision')
    base = marker.read_text().strip() if marker.exists() else None
    if not base:
        return True
    try:
        paths = subprocess.check_output(
            ['git', 'diff', '--name-only', '--no-renames', '-z', base, 'HEAD'],
            stderr=subprocess.DEVNULL).decode('utf-8').split('\0')
        return any(relevant(path) for path in paths if path)
    except (subprocess.CalledProcessError, UnicodeDecodeError):
        return True


if __name__ == '__main__':
    print(str(required()).lower())
