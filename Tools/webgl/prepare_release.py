"""Prepare version notice and container fullscreen on an unpublished WebGL build."""
from pathlib import Path
import argparse
import shutil


def prepare(staged):
    staged = Path(staged)
    index = staged / 'index.html'
    page = index.read_text(encoding='utf-8').replace(
        '</body>', '<script src="release-info.js"></script></body>')
    page = page.replace('</head>', '<style>#unity-container.unity-desktop{width:min(960px,100vw)}'
        '#unity-container.unity-desktop #unity-canvas{width:100%!important;height:auto!important}'
        '#unity-container:fullscreen{width:100vw!important;height:100vh;transform:none;left:0;top:0}'
        '#unity-container:fullscreen #unity-canvas{width:100%!important;height:100%!important}'
        '#unity-container:fullscreen #unity-footer{display:none}'
        '</style></head>')
    # Fullscreen the container so native IME input remains visible above the canvas.
    page = page.replace('unityInstance.SetFullscreen(1);',
        'document.querySelector("#unity-container").requestFullscreen().catch(console.warn);')
    index.write_text(page, encoding='utf-8')
    shutil.copyfile(Path(__file__).with_name('release-info.js'), staged / 'release-info.js')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('directory', type=Path)
    prepare(parser.parse_args().directory)
