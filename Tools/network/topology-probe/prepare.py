"""Create a NEW disposable Unity project using the installed Fusion SDK. No downloads."""
import argparse
import json
from pathlib import Path
import shutil

parser = argparse.ArgumentParser()
parser.add_argument('repository', type=Path)
parser.add_argument('package_cache', type=Path)
parser.add_argument('output', type=Path)
args = parser.parse_args()
root = args.output.resolve()
root.mkdir(parents=True, exist_ok=False)
(root / 'Assets/Editor').mkdir(parents=True)
(root / 'Assets/Plugins').mkdir()
(root / 'Packages').mkdir()
(root / 'ProjectSettings').mkdir()
for folder in ('Fusion', 'PhotonRealtime', 'PhotonLibs'):
    shutil.copytree(args.repository / 'Assets/Photon' / folder, root / 'Assets/Photon' / folder)
for filename in ('TopologyProbe.cs', 'ProbeState.cs'):
    shutil.copyfile(Path(__file__).parent / filename, root / 'Assets' / filename)
shutil.copyfile(Path(__file__).with_name('ProbeBuild.cs'), root / 'Assets/Editor/ProbeBuild.cs')
shutil.copyfile(Path(__file__).with_name('ProbeStatus.jslib'), root / 'Assets/Plugins/ProbeStatus.jslib')
manifest = json.loads((args.repository / 'Packages/manifest.json').read_text(encoding='utf-8'))
dependencies = {k: v for k, v in manifest['dependencies'].items() if k.startswith('com.unity.modules.')}
for package in ('com.cysharp.unitask', 'com.unity.nuget.mono-cecil', 'com.unity.nuget.newtonsoft-json',
                'com.unity.ugui', 'com.unity.test-framework', 'com.unity.ext.nunit'):
    source = next(args.package_cache.glob(package + '@*'))
    destination = root / 'Packages' / package
    shutil.copytree(source, destination)
(root / 'Packages/manifest.json').write_text(json.dumps({'dependencies': dependencies}, indent=2), encoding='utf-8')
shutil.copyfile(args.repository / 'ProjectSettings/ProjectVersion.txt', root / 'ProjectSettings/ProjectVersion.txt')
shutil.copyfile(args.repository / 'ProjectSettings/ProjectSettings.asset', root / 'ProjectSettings/ProjectSettings.asset')
# Copy only the SDK config into the isolated project; remove game-specific weaving entries.
config_path = root / 'Assets/Photon/Fusion/Resources/NetworkProjectConfig.fusion'
config = json.loads(config_path.read_text(encoding='utf-8'))
config['AssembliesToWeave'] = ['Assembly-CSharp']
config['AllowClientServerModesInWebGL'] = True
config_path.write_text(json.dumps(config, indent=2), encoding='utf-8')
print(root)
