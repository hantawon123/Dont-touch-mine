"""Select the active test release for this project's Editor Play mode only."""
import argparse
from pathlib import Path
import re
from urllib.parse import urljoin
from urllib.request import urlopen


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('project', type=Path)
    parser.add_argument('--origin', default='http://localhost:4291')
    args = parser.parse_args()
    if not (args.project / 'ProjectSettings/ProjectVersion.txt').is_file():
        raise SystemExit('Use the Unity project root')
    # Follow the same ready-release redirect as a new browser. Fetch that pinned
    # version, so an activation between these requests cannot mix release data.
    with urlopen(args.origin.rstrip('/') + '/', timeout=20) as response:
        version_url = urljoin(response.url, 'version.txt')
    with urlopen(version_url, timeout=20) as response:
        version = response.read(129).decode('utf-8-sig').strip()
    if not re.fullmatch(r'[A-Za-z0-9][A-Za-z0-9._-]{0,63}', version):
        raise SystemExit('Invalid test build version')
    path = args.project / 'UserSettings/ServerFlowVersion.txt'
    path.parent.mkdir(exist_ok=True)
    temporary = path.with_suffix('.tmp')
    temporary.write_text(version + '\n', encoding='utf-8')
    temporary.replace(path)
    print('Editor test version:', version, '(restart Play mode; select the server region)')


if __name__ == '__main__':
    main()
