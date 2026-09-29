"""Publish an immutable WebGL release, or point new visitors at an older release."""
import argparse
import fcntl
import json
import os
from pathlib import Path
import re
import shutil
import tempfile
from prepare_release import prepare


def revision(value):
    if not re.fullmatch(r'[a-f0-9]{40}', value):
        raise ValueError('Expected a full lowercase Git commit SHA')
    return value


def write_atomic(path, content):
    with tempfile.NamedTemporaryFile(dir=path.parent, delete=False) as stream:
        staging = Path(stream.name)
        stream.write(content)
    staging.chmod(0o644)
    os.replace(staging, path)


def validate_build(directory, sha):
    if (directory / 'version.txt').read_text().strip() != sha:
        raise ValueError('Build version does not match the release commit')
    if not (directory / 'index.html').is_file():
        raise ValueError('Missing WebGL entry point')
    for suffix in ('.wasm.gz', '.data.gz', '.framework.js.gz', '.loader.js'):
        if not any(path.is_file() and path.stat().st_size > 0
                   for path in (directory / 'Build').glob('*' + suffix)):
            raise ValueError('Missing or empty WebGL build artifact: ' + suffix)


def publish(root, sha, sequence=None, source=None):
    sha = revision(sha)
    root = Path(root).resolve()
    root.mkdir(parents=True, exist_ok=True)
    with (root / '.publish.lock').open('a') as lock:
        fcntl.flock(lock, fcntl.LOCK_EX)
        pointer = root / 'current.json'
        previous = json.loads(pointer.read_text()) if pointer.exists() else {'sequence': 0}
        if source is not None and sequence <= previous['sequence']:
            print('Skipping an older or already published build')
            return
        releases = root / 'releases'
        releases.mkdir(exist_ok=True)
        destination = releases / sha
        if source is not None:
            source = Path(source).resolve()
            validate_build(source, sha)
            if not destination.exists():
                with tempfile.TemporaryDirectory(dir=releases, prefix='.staging-') as temporary:
                    staged = Path(temporary) / 'release'
                    shutil.copytree(source, staged)
                    prepare(staged)
                    # Jenkins may run with a restrictive umask; nginx needs read access.
                    for path in staged.rglob('*'):
                        path.chmod(0o755 if path.is_dir() else 0o644)
                    staged.chmod(0o755)
                    staged.rename(destination)
        elif not (destination / 'index.html').is_file():
            raise ValueError('Rollback target is not a published release')
        # Existing immutable releases must still be complete before selecting them.
        validate_build(destination, sha)
        site = root / 'site'
        site.mkdir(exist_ok=True)
        write_atomic(site / 'index.html', Path(__file__).with_name('index.html').read_bytes())
        # Rollback keeps the watermark so delayed old jobs cannot undo it.
        current = {'revision': sha, 'sequence': sequence if source else previous['sequence']}
        write_atomic(pointer, json.dumps(current).encode())
        print('Published ' + sha)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('root')
    parser.add_argument('revision')
    parser.add_argument('--source')
    parser.add_argument('--sequence', type=int)
    args = parser.parse_args()
    if args.source and (args.sequence is None or args.sequence < 1):
        parser.error('--source requires a positive --sequence')
    publish(args.root, args.revision, args.sequence, args.source)
