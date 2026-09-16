#!/bin/bash
# Orchestration checks without Unity, credentials or real Docker.
set -euo pipefail
support=$(cd "$(dirname "$0")" && pwd)
fixture=$(mktemp -d)
trap 'rm -rf "$fixture" "${fixture}@server-library"' EXIT
cd "$fixture"
mkdir -p bin ProjectSettings config license/licenses Logs
touch ProjectSettings/ProjectVersion.txt config/PhotonAppSettings.asset license/licenses/UnityEntitlementLicense.xml
export CLIENT_CONFIG_DIR="$fixture/config" CLIENT_UNITY_HOME="$fixture/license"
export PATH="$fixture/bin:$PATH"
cat > bin/git <<'SH'
#!/bin/sh
printf '%040d\n' 1
SH
cat > bin/docker <<'SH'
#!/bin/sh
printf '%s\n' "$*" >> calls.log
case "$*" in
  ps*) printf '%s\n' own-container ;;
  *-runTests*) printf '<test-run result="%s" total="1" />' "${TEST_RESULT:-Passed}" > Logs/client-contract-results.xml ;;
  *Game.Editor.DedicatedServerBuild.Build*) exit 9 ;;
esac
SH
chmod +x bin/git bin/docker
export RELEASE_REVISION=$(git rev-parse HEAD)
mkdir -p Library/ShaderCache
touch Library/ShaderCache/retained
status=0
bash "$support/build.sh" >/dev/null 2>&1 || status=$?
test "$status" = 9
! grep -q -- '-buildTarget Win64' calls.log
grep -Fq "src=${fixture}@server-library,dst=/workspace/Library" calls.log
grep -q BEE_CACHE_DIRECTORY=/cache/bee-server calls.log
grep -q 'stop -t 10 d205-ci-' calls.log
grep -q $'total\t' Logs/client-timings.tsv
test -f Library/ShaderCache/retained
: > calls.log
RELEASE_REVISION=bad bash "$support/build.sh" >/dev/null 2>&1 && exit 1
test ! -s calls.log
bash "$support/container.sh" cleanup
grep -q 'ps -aq --filter label=d205.client-ci=' calls.log
grep -q 'stop -t 10 own-container' calls.log
echo 'PASS: revision gate, Linux-only build, retained cache, exit status, scoped cleanup'
