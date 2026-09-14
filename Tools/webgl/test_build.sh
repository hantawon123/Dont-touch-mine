#!/bin/bash
# Exercise orchestration without launching Unity, Docker or accessing real credentials.
set -euo pipefail
script=$(cd "$(dirname "$0")" && pwd)/build.sh
fixture=$(mktemp -d)
trap 'rm -rf "$fixture"' EXIT
cd "$fixture"
mkdir -p bin ProjectSettings config license/licenses Logs
touch ProjectSettings/ProjectVersion.txt config/PhotonAppSettings.asset license/licenses/UnityEntitlementLicense.xml
export WEBGL_CONFIG_DIR="$fixture/config" WEBGL_UNITY_HOME="$fixture/license"
export PATH="$fixture/bin:$PATH"
cat > bin/git <<'SH'
#!/bin/sh
printf '%040d\n' 1
SH
cat > bin/docker <<'SH'
#!/bin/sh
printf '%s\n' "$*" >> calls.log
case "$*" in
  *-runTests*)
    printf '<test-run result="%s" total="1" />' "${TEST_RESULT:-Passed}" > Logs/webgl-contract-results.xml
    ;;
  *Game.Editor.DedicatedServerBuild.Build*)
    test "${SERVER_FAIL:-0}" != 1 || exit 1
    mkdir -p Builds/Server
    touch Builds/Server/GameServer.x86_64
    git rev-parse HEAD > Builds/Server/version.txt
    ;;
  *-executeMethod*)
    mkdir -p Builds/WebGL
    printf '<html><head></head><body></body></html>' > Builds/WebGL/index.html
    git rev-parse HEAD > Builds/WebGL/version.txt
    ;;
esac
SH
chmod +x bin/git bin/docker
# A fresh run must discard stale logs, retain caches and gate compilation on tests.
echo stale > Logs/webgl-build.log
TEST_RESULT=Failed bash "$script" >/dev/null 2>&1 && exit 1
test ! -f Logs/webgl-build.log
! grep -q -- -executeMethod calls.log
grep -q $'total\t' Logs/webgl-timings.tsv
touch Library/WebGLCiCache/bee/retained
bash "$script" >/dev/null
test -f Library/WebGLCiCache/bee/retained
grep -q BEE_CACHE_DIRECTORY=/cache/bee calls.log
grep -q NUGET_PACKAGES=/cache/nuget calls.log
grep -Fq -- "--tmpfs /home/unity/.config/unity3d:uid=$(id -u),gid=$(id -g),mode=700" calls.log
grep -q $'build\t' Logs/webgl-timings.tsv
test -f Builds/WebGL/version.txt
test -f Builds/WebGL/release-info.js
grep -q release-info.js Builds/WebGL/index.html
test -f Builds/Server/GameServer.x86_64
cmp Builds/WebGL/version.txt Builds/Server/version.txt
grep -q -- '-buildTarget Linux64 -standaloneBuildSubtarget Server' calls.log
SERVER_FAIL=1 bash "$script" >/dev/null 2>&1 && exit 1
: > calls.log
WEBGL_TEST_ONLY=1 bash "$script" >/dev/null
! grep -q -- -executeMethod calls.log
test ! -f Logs/webgl-build.log
# Early validation failure must not expose the preceding build's log as current.
WEBGL_CONFIG_DIR="$fixture/missing" bash "$script" >/dev/null 2>&1 && exit 1
test ! -f Logs/webgl-build.log
echo 'Build gating, cache mounts, timings and stale-log cleanup passed'
