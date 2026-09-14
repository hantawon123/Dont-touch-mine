"""Refresh an isolated full game project and install validation-only entry points."""
import argparse
from pathlib import Path
import shutil

parser = argparse.ArgumentParser()
parser.add_argument('repository', type=Path)
parser.add_argument('lab', type=Path)
args = parser.parse_args()
source, lab = args.repository.resolve(), args.lab.resolve()
if source == lab or source in lab.parents or lab in source.parents:
    raise SystemExit('Use an independent directory outside the repository')
for folder in ('Assets', 'Packages', 'ProjectSettings'):
    shutil.copytree(source / folder, lab / folder, dirs_exist_ok=True,
                    ignore=shutil.ignore_patterns('Supermarket_copy.unity', 'Supermarket_copy.unity.meta'))
support = Path(__file__).parent
shutil.copyfile(support / 'ValidationDriver.cs', lab / 'Assets/_Game/Bootstrap/ServerFlowValidation.cs')
(lab / 'Assets/Editor').mkdir(exist_ok=True)
shutil.copyfile(support / 'ValidationBuild.cs', lab / 'Assets/Editor/ServerFlowBuild.cs')
scope = lab / 'Assets/_Game/Bootstrap/ProjectLifetimeScope.cs'
text = scope.read_text(encoding='utf-8-sig')
endpoint = 'var endpoint = new BackendEndpoint(baseUrl);'
notifications = 'var frames = RegisterNotifications(builder, endpoint, session);'
if text.count(endpoint) != 1 or text.count(notifications) != 1:
    raise SystemExit('Backend wiring changed; review local preview injection')
text = text.replace(endpoint, '''#if UNITY_WEBGL && !UNITY_EDITOR
            baseUrl = new System.Uri(UnityEngine.Application.absoluteURL).GetLeftPart(System.UriPartial.Authority);
#endif
            ''' + endpoint)
text = text.replace(notifications, '''#if UNITY_WEBGL && !UNITY_EDITOR
            var frames = RegisterNotifications(builder, new BackendEndpoint(), session);
#else
            ''' + notifications + '\n#endif')
marker = 'protected override void Configure(IContainerBuilder builder)\n        {'
if text.count(marker) != 1:
    raise SystemExit('Project scope changed; review validation injection')
scope.write_text(text.replace(marker, marker + '\n            builder.RegisterEntryPoint<ServerFlowValidation>();'), encoding='utf-8')
print('Prepared isolated game validation project')
