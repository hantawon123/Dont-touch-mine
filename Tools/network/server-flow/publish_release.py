"""Publish a matched pair, then wait for the running host's readiness decision."""
import argparse
import re
import time
import uuid
from pathlib import Path
from releases import atomic_json, read_json, release_path, stage, validate


def publish(root, revision, web, server, timeout=150):
    root = Path(root)
    if not re.fullmatch(r'[a-f0-9]{40}', revision):
        raise ValueError('Use the full Git commit as the paired release version')
    for folder in (Path(web), Path(server)):
        if (folder / 'version.txt').read_text().strip() != revision:
            raise ValueError('Both build versions must match the source commit')
    if not release_path(root, revision).exists():
        stage(root, revision, web, server, 'GameServer.x86_64')
    else:
        # Rebuilding a commit must never replace assets used by existing tabs.
        if validate(root, revision)['version'] != revision:
            raise ValueError('Existing immutable release has a different version')
    nonce = uuid.uuid4().hex
    atomic_json(root / 'request.json', {'release': revision, 'nonce': nonce})
    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline:
        status = read_json(root / 'status.json', {})
        if (status.get('request') or {}).get('nonce') == nonce:
            if status.get('outcome') == 'activated' and status.get('active') == revision:
                return
            if status.get('outcome') in ('failed', 'control-error'):
                raise RuntimeError('Host rejected the release; previous active route retained')
        time.sleep(1)
    raise TimeoutError('Host did not confirm activation; inspect status before retrying')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('root', type=Path)
    parser.add_argument('revision')
    parser.add_argument('--web', required=True, type=Path)
    parser.add_argument('--server', required=True, type=Path)
    args = parser.parse_args()
    publish(args.root, args.revision, args.web, args.server)
    print('Matched server/WebGL release activated:', args.revision)
