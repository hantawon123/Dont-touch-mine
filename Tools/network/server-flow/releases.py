"""Immutable local releases and atomic control files; no network admin endpoint."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import uuid


def read_json(path, default=None):
    try:
        return json.loads(Path(path).read_text(encoding='utf-8-sig'))
    except FileNotFoundError:
        return default


def atomic_json(path, value):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    temp = path.with_name(path.name + '.' + uuid.uuid4().hex + '.tmp')
    try:
        temp.write_text(json.dumps(value, indent=2) + '\n', encoding='utf-8')
        os.replace(temp, path)
    finally:
        temp.unlink(missing_ok=True)


def release_path(root, name):
    if not re.fullmatch(r'[A-Za-z0-9][A-Za-z0-9._-]{0,63}', name):
        raise ValueError('Use a short alphanumeric release ID')
    return Path(root) / 'releases' / name


def validate(root, name):
    path = release_path(root, name)
    manifest = read_json(path / 'release.json')
    if not manifest or manifest['id'] != name:
        raise ValueError('Release manifest missing or mismatched')
    for relative, digest in manifest['files'].items():
        file = (path / relative).resolve()
        if not file.is_relative_to(path.resolve()) or not file.is_file():
            raise ValueError('Invalid release file')
        with file.open('rb') as stream:
            if hashlib.file_digest(stream, 'sha256').hexdigest() != digest:
                raise ValueError('Release file changed: ' + relative)
    if (path / 'web/version.txt').read_text().strip() != manifest['version']:
        raise ValueError('Web version mismatch')
    if (path / 'server/version.txt').read_text().strip() != manifest['version']:
        raise ValueError('Server version mismatch')
    if not (path / 'web/index.html').is_file() or not (path / 'server' / manifest['executable']).is_file():
        raise ValueError('Incomplete release')
    return manifest


def stage(root, name, web, server, executable):
    target = release_path(root, name)
    if target.exists():
        raise ValueError('Never overwrite a published release; use another ID')
    if Path(executable).name != executable:
        raise ValueError('Executable must be a filename')
    web, server = Path(web), Path(server)
    version = (web / 'version.txt').read_text().strip()
    if not version or version != (server / 'version.txt').read_text().strip():
        raise ValueError('Build WebGL and server with the same SERVER_FLOW_VERSION')
    target.parent.mkdir(parents=True, exist_ok=True)
    temporary = target.with_name('.stage-' + uuid.uuid4().hex)
    try:
        if any(p.is_symlink() for source in (web, server) for p in source.rglob('*')):
            raise ValueError('Release sources must not contain symlinks')
        shutil.copytree(web, temporary / 'web')
        shutil.copytree(server, temporary / 'server')
        (temporary / 'server' / executable).chmod(0o755)
        files = {}
        for file in temporary.rglob('*'):
            if file.is_symlink():
                raise ValueError('Release symlinks are not supported')
            if file.is_file():
                with file.open('rb') as stream:
                    files[file.relative_to(temporary).as_posix()] = hashlib.file_digest(stream, 'sha256').hexdigest()
        atomic_json(temporary / 'release.json', dict(id=name, version=version, executable=executable, files=files))
        # Verify all required artifacts before making the directory visible.
        if not (temporary / 'web/index.html').is_file():
            raise ValueError('WebGL index missing')
        temporary.rename(target)
    finally:
        if temporary.exists():
            if not temporary.resolve().is_relative_to(target.parent.resolve()) or not temporary.name.startswith('.stage-'):
                raise ValueError('Refusing cleanup outside staging root')
            shutil.rmtree(temporary)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('root', type=Path)
    sub = parser.add_subparsers(dest='command', required=True)
    create = sub.add_parser('stage')
    create.add_argument('release')
    create.add_argument('--web', required=True)
    create.add_argument('--server', required=True)
    create.add_argument('--executable', default='ServerFlow988.x86_64')
    activate = sub.add_parser('activate')
    activate.add_argument('release')
    sub.add_parser('status')
    args = parser.parse_args()
    if args.command == 'stage':
        stage(args.root, args.release, args.web, args.server, args.executable)
        print('Staged:', args.release)
    elif args.command == 'activate':
        validate(args.root, args.release)
        atomic_json(args.root / 'request.json', dict(release=args.release, nonce=uuid.uuid4().hex))
        print('Requested; active release changes only after server readiness. Check status.')
    else:
        print(json.dumps(read_json(args.root / 'status.json', {}), indent=2))


if __name__ == '__main__':
    main()
