"""Package only Windows runtime artifacts as a download site for the paired host."""
import argparse
import hashlib
from pathlib import Path
import re
import zipfile


def package(client, output, revision):
    client, output = Path(client), Path(output)
    if not re.fullmatch(r'[a-f0-9]{40}', revision):
        raise ValueError('Use the full source commit')
    if (client / 'version.txt').read_text().strip() != revision:
        raise ValueError('Client version mismatch')
    required = ('Game.exe', 'UnityPlayer.dll', 'Game_Data/globalgamemanagers')
    if not all((client / name).is_file() for name in required):
        raise ValueError('Incomplete Windows player')
    roots = ('Game.exe', 'UnityPlayer.dll', 'UnityCrashHandler64.exe', 'Game_Data',
             'MonoBleedingEdge', 'version.txt')
    files = []
    for name in roots:
        path = client / name
        for file in ([path] if path.is_file() else path.rglob('*')):
            if file.is_symlink() or not file.resolve().is_relative_to(client.resolve()):
                raise ValueError('Do not package symlinks')
            if file.is_file():
                if file.name.startswith('.env') or file.suffix in ('.pem', '.key'):
                    raise ValueError('Unexpected credential file in player')
                files.append(file)
    output.mkdir(parents=True, exist_ok=False)
    name = f'KeepIt-Windows-{revision[:12]}.zip'
    with zipfile.ZipFile(output / name, 'w', zipfile.ZIP_DEFLATED, compresslevel=1) as archive:
        for file in sorted(files):
            archive.write(file, file.relative_to(client).as_posix())
    with (output / name).open('rb') as stream:
        digest = hashlib.file_digest(stream, 'sha256').hexdigest()
    (output / 'SHA256SUMS.txt').write_text(f'{digest}  {name}\n', encoding='utf-8')
    (output / 'version.txt').write_text(revision + '\n', encoding='utf-8')
    (output / 'index.html').write_text(f'''<!doctype html>
<html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>KEEP IT · Windows 다운로드</title>
<style>body{{font:18px system-ui;background:#111827;color:#eee;max-width:720px;margin:12vh auto;padding:24px}}a{{color:#7dd3fc}}code{{overflow-wrap:anywhere}}</style>
<h1>KEEP IT</h1><p>Windows 64비트 클라이언트</p>
<p><a href="{name}" download>게임 다운로드 (ZIP)</a></p>
<p>압축을 모두 푼 뒤 Game.exe를 실행하세요. 최신 서버와 같은 버전입니다.</p>
<p>이전 버전에서 방을 찾을 수 없다면 최신 파일을 다시 받아주세요.</p>
<p>버전: <code>{revision}</code></p><a href="SHA256SUMS.txt">SHA-256 확인</a>
</html>''', encoding='utf-8')
    return output / name


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('client', type=Path)
    parser.add_argument('output', type=Path)
    parser.add_argument('revision')
    args = parser.parse_args()
    print(package(args.client, args.output, args.revision).name)
