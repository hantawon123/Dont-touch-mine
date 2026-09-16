"""Package only Windows runtime artifacts as a download site for the paired host."""
import argparse
from datetime import datetime, timedelta, timezone
import hashlib
from pathlib import Path
import re
import shutil
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
    (output / 'SHA256SUMS.txt').write_text(f'{digest}  {name}\n', encoding='utf-8', newline='\n')
    (output / 'version.txt').write_text(revision + '\n', encoding='utf-8', newline='\n')
    template = Path(__file__).with_name('site') / 'play.html'
    html = template.read_text(encoding='utf-8')
    values = {
        'ARCHIVE': name,
        'VERSION': revision[:12],
        'ZIP_SIZE': f'{(output / name).stat().st_size / 1024**2:,.1f} MiB',
        'INSTALL_SIZE': f'{sum(file.stat().st_size for file in files) / 1024**2:,.1f} MiB',
        'DATE': datetime.now(timezone(timedelta(hours=9))).strftime('%Y.%m.%d'),
    }
    for key, value in values.items():
        html = html.replace('@@' + key + '@@', value)
    (output / 'index.html').write_text(html, encoding='utf-8')
    hero = Path(__file__).resolve().parents[2] / 'docs/design/concept/main-screen-concept.png'
    shutil.copyfile(hero, output / 'hero.png')
    shutil.copyfile(template.with_name('beta-test-favicon.png'), output / 'beta-test-favicon.png')
    for step in range(1, 5):
        asset_name = f'step-{step}.png'
        shutil.copyfile(template.with_name(asset_name), output / asset_name)
    return output / name


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('client', type=Path)
    parser.add_argument('output', type=Path)
    parser.add_argument('revision')
    args = parser.parse_args()
    print(package(args.client, args.output, args.revision).name)
