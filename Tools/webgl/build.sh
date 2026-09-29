#!/bin/bash
set -euo pipefail
project=$(pwd -P)
support=$(cd "$(dirname "$0")" && pwd)
revision=$(git rev-parse HEAD)
cache="$project/Library/WebGLCiCache"
mkdir -p Logs "$cache/bee" "$cache/nuget" "$cache/tools"
# Clear reports before even validating credentials so a failed run cannot archive old success.
rm -f Logs/webgl-build.log Logs/server-build.log Logs/webgl-tests.log Logs/webgl-contract-results.xml Logs/webgl-build-report.json
printf 'phase\tseconds\texit_code\n' > Logs/webgl-timings.tsv
build_started=$SECONDS
trap 'status=$?; printf "total\t%s\t%s\n" "$((SECONDS-build_started))" "$status" >> Logs/webgl-timings.tsv' EXIT
timed() {
    local phase=$1 started=$SECONDS status
    shift
    if "$@"; then status=0; else status=$?; fi
    printf '%s\t%s\t%s\n' "$phase" "$((SECONDS-started))" "$status" >> Logs/webgl-timings.tsv
    return "$status"
}
config=${WEBGL_CONFIG_DIR:-/var/lib/jenkins/.config/unity-webgl}
unity_home=${WEBGL_UNITY_HOME:-/var/lib/jenkins/.config/unity3d/Unity}
test -f ProjectSettings/ProjectVersion.txt
test -f "$config/PhotonAppSettings.asset"
test -f "$unity_home/licenses/UnityEntitlementLicense.xml"
mkdir -p Logs Assets/Photon/Fusion/Resources
cp "$config/PhotonAppSettings.asset" Assets/Photon/Fusion/Resources/PhotonAppSettings.asset
uid=$(id -u)
gid=$(id -g)

# Restore R3 before Unity attempts to compile scripts on a fresh checkout.
timed restore docker run --rm --cpus=1 --memory=1g --user "$uid:$gid" \
    -e HOME=/tmp/dotnet-home -e DOTNET_CLI_TELEMETRY_OPTOUT=1 -e DOTNET_ROLL_FORWARD=Major \
    -e NUGET_PACKAGES=/cache/nuget -v "$cache:/cache" \
    -v "$project:/workspace" -w /workspace \
    mcr.microsoft.com/dotnet/sdk:8.0 \
    sh -ec 'test -x /cache/tools/4.5.0/nugetforunity || dotnet tool install NuGetForUnity.Cli --version 4.5.0 --tool-path /cache/tools/4.5.0; /cache/tools/4.5.0/nugetforunity restore /workspace'

# This is the actual EC2 host identity used during official activation.
# Never copy another computer's machine-id or change the license XML.
# The nested license bind mount creates root-owned parents; keep preferences writable.
run_unity() {
local target=$1 image=$2
shift 2
docker run --rm --cpus=3 --cpu-shares=1024 --memory=8g --memory-swap=8g \
    --user "$uid:$gid" -e HOME=/home/unity -e WEBGL_REVISION="$revision" \
    -e BEE_CACHE_DIRECTORY=/cache/bee -e WEBGL_FAST_BUILD="${WEBGL_FAST_BUILD:-0}" \
    --mount "type=bind,src=$cache,dst=/cache" \
    --tmpfs "/home/unity:uid=$uid,gid=$gid,mode=700" \
    --tmpfs "/home/unity/.config/unity3d:uid=$uid,gid=$gid,mode=700" \
    --mount type=bind,src=/etc/machine-id,dst=/etc/machine-id,readonly \
    --mount "type=bind,src=$unity_home,dst=/home/unity/.config/unity3d/Unity" \
    --mount "type=bind,src=$project,dst=/workspace" -w /workspace \
    "$image" \
    unity-editor -batchmode -nographics -projectPath /workspace -buildTarget "$target" "$@"
}

web_image=unityci/editor:ubuntu-6000.3.22f1-webgl-3@sha256:509149d9a3bf36e84ce6e2916f7de6168d3cded05502c3129fc511de6768a42a
server_image=unityci/editor:ubuntu-6000.3.22f1-linux-il2cpp-3.2.2@sha256:bd9f0c77473bc842423236ec1498f180380f734dde521397e0fac2319865e87a
timed tests run_unity WebGL "$web_image" -runTests -testPlatform EditMode \
    -testFilter Game.Architecture.Tests.NetworkContractTests \
    -testResults /workspace/Logs/webgl-contract-results.xml -logFile - 2>&1 | tee Logs/webgl-tests.log
python3 -c 'import xml.etree.ElementTree as ET; result = ET.parse("Logs/webgl-contract-results.xml").getroot(); assert result.get("result") == "Passed" and int(result.get("total", "0")) > 0, "Unity contract tests did not pass"'
if [ "${WEBGL_TEST_ONLY:-0}" = 1 ]; then exit 0; fi
timed build run_unity WebGL "$web_image" -quit -executeMethod Game.Editor.WebBuild.Build -logFile - 2>&1 | tee Logs/webgl-build.log
test -f Builds/WebGL/index.html
test "$(cat Builds/WebGL/version.txt)" = "$revision"

# A release always contains both targets from this exact source revision.
timed server run_unity Linux64 "$server_image" -standaloneBuildSubtarget Server \
    -quit -executeMethod Game.Editor.DedicatedServerBuild.Build -logFile - 2>&1 | tee Logs/server-build.log
test -f Builds/Server/GameServer.x86_64
test "$(cat Builds/Server/version.txt)" = "$revision"

python3 "$support/prepare_release.py" Builds/WebGL
