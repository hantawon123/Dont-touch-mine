"""Run with python3 Tools/webgl/test_publish.py on Linux."""
import json
from pathlib import Path
import tempfile
from publish import publish

with tempfile.TemporaryDirectory() as directory:
    root = Path(directory) / 'site'
    build = Path(directory) / 'build'
    (build / 'Build').mkdir(parents=True)
    (build / 'index.html').write_text('<html><head></head><body><script>unityInstance.SetFullscreen(1);</script></body></html>')
    for suffix in ('.wasm.gz', '.data.gz', '.framework.js.gz', '.loader.js'):
        (build / 'Build' / ('test' + suffix)).write_bytes(b'fixture')
    first, second = 'a' * 40, 'b' * 40
    for sha, number in ((first, 1), (second, 2)):
        (build / 'version.txt').write_text(sha)
        publish(root, sha, number, build)
    pointer = root / 'current.json'
    assert json.loads(pointer.read_text())['revision'] == second
    publish(root, first, 1, build)
    assert json.loads(pointer.read_text())['revision'] == second
    publish(root, first)
    assert json.loads(pointer.read_text()) == {'revision': first, 'sequence': 2}
    before = pointer.read_bytes()
    for invalid in ('../escape', 'c' * 40):
        try:
            publish(root, invalid)
        except ValueError:
            pass
        else:
            raise AssertionError('Invalid rollback was accepted')
        assert pointer.read_bytes() == before
    try:
        publish(root, second, 3, build / 'missing')
    except FileNotFoundError:
        pass
    else:
        raise AssertionError('Missing build was accepted')
    assert pointer.read_bytes() == before
    # Corrupt published targets must not replace the healthy current release.
    damaged = root / 'releases' / second
    wasm = damaged / 'Build' / 'test.wasm.gz'
    original = wasm.read_bytes()
    for operation in ('missing', 'empty', 'wrong-version'):
        if operation == 'missing':
            wasm.unlink()
        elif operation == 'empty':
            wasm.write_bytes(b'')
        else:
            (damaged / 'version.txt').write_text(first)
        for source, sequence in ((None, None), (build, 3)):
            try:
                publish(root, second, sequence, source)
            except (ValueError, FileNotFoundError):
                pass
            else:
                raise AssertionError('Corrupt release was selected: ' + operation)
            assert pointer.read_bytes() == before
        wasm.write_bytes(original)
        (damaged / 'version.txt').write_text(second)
    publish(root, second)
    assert json.loads(pointer.read_text()) == {'revision': second, 'sequence': 2}
    assert 'release-info.js' in (root / 'releases' / first / 'index.html').read_text()
    assert 'width:min(960px,100vw)' in (root / 'releases' / first / 'index.html').read_text()
    assert '.requestFullscreen()' in (root / 'releases' / first / 'index.html').read_text()
    assert 'unityInstance.SetFullscreen(1)' not in (root / 'releases' / first / 'index.html').read_text()
print('Publication, ordering, rollback and failed-deployment checks passed')
