$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$project = (Get-Location).Path
& python -m unittest discover -s Tools/client -p 'test_*.py'
if ($LASTEXITCODE -ne 0) { throw 'Windows packaging tests failed.' }
$revision = (& git rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or $revision -notmatch '^[a-f0-9]{40}$' -or $revision -ne $env:RELEASE_REVISION) {
    throw 'Windows checkout does not match the resolved release revision.'
}
$unity = $env:CLIENT_UNITY_EXE
if (!$unity) { $unity = 'C:\Program Files\Unity\Hub\Editor\6000.3.22f1\Editor\Unity.exe' }
$config = $env:CLIENT_CONFIG_DIR
if (!$config) { $config = Join-Path $env:USERPROFILE '.d205-unity-ci\config' }
$cache = Join-Path $project 'Library\ClientCiCache'
New-Item -ItemType Directory -Force Logs,$cache | Out-Null
Get-ChildItem Logs -Filter 'client-*' -File | Remove-Item
if (!(Test-Path -LiteralPath $unity)) { throw "Unity not installed: $unity" }
if (!(Test-Path -LiteralPath "$config\PhotonAppSettings.asset")) { throw 'Photon configuration is missing.' }
& "$PSScriptRoot/stop-windows-build.ps1"
Copy-Item -LiteralPath "$config\PhotonAppSettings.asset" -Destination 'Assets/Photon/Fusion/Resources/PhotonAppSettings.asset' -Force
$env:DOTNET_ROLL_FORWARD = 'Major'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:NUGET_PACKAGES = Join-Path $cache 'nuget'
$tool = Join-Path $cache 'tools\4.5.0'
if (!(Test-Path "$tool\nugetforunity.exe")) {
    & dotnet tool install NuGetForUnity.Cli --version 4.5.0 --tool-path $tool
    if ($LASTEXITCODE -ne 0) { throw 'NuGetForUnity installation failed.' }
}
& "$tool\nugetforunity.exe" restore $project
if ($LASTEXITCODE -ne 0) { throw 'Package restore failed.' }
$env:CLIENT_REVISION = $revision
$env:BEE_CACHE_DIRECTORY = Join-Path $cache 'bee'
"phase`tseconds" | Set-Content Logs/client-native-timings.tsv
function Invoke-Unity([string]$phase, [string[]]$arguments) {
    $timer = [Diagnostics.Stopwatch]::StartNew()
    $log = Join-Path $project "Logs/client-$phase.log"
    Write-Output "[CI] Unity $phase started; log=$log"
    $process = Start-Process -FilePath $unity -ArgumentList (@('-batchmode','-nographics','-projectPath',"`"$project`"",'-buildTarget','Win64','-logFile',"`"$log`"") + $arguments) -WindowStyle Hidden -PassThru
    $null = $process.Handle
    try {
        @{ Id=$process.Id; Started=$process.StartTime.ToUniversalTime().Ticks.ToString(); Path=$unity } |
            ConvertTo-Json | Set-Content Logs/windows-unity-process.json
        while (!$process.WaitForExit(60000)) {
            Write-Output "[CI] Unity $phase running: $([math]::Round($timer.Elapsed.TotalSeconds)) seconds"
        }
        $process.Refresh()
        if ($process.ExitCode -ne 0) { throw "Unity $phase failed ($($process.ExitCode)); see $log" }
    } finally {
        & "$PSScriptRoot/stop-windows-build.ps1"
        "$phase`t$([math]::Round($timer.Elapsed.TotalSeconds,2))" | Add-Content Logs/client-native-timings.tsv
    }
}
Invoke-Unity 'tests' @('-executeMethod','Game.Editor.ClientBuild.PrepareTests','-runTests','-testPlatform','EditMode','-testFilter','Game.Architecture.Tests.NetworkContractTests','-testResults',"`"$project/Logs/client-contract-results.xml`"")
[xml]$tests = Get-Content Logs/client-contract-results.xml
if ($tests.'test-run'.result -ne 'Passed' -or [int]$tests.'test-run'.total -le 0) { throw 'Unity contract tests did not pass.' }
if ($env:CLIENT_TEST_ONLY -eq '1') { return }
foreach ($relative in @('Builds/Client','Builds/Download')) {
    $output = [IO.Path]::GetFullPath((Join-Path $project $relative))
    if (!$output.StartsWith($project.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Output outside workspace.' }
    if (Test-Path -LiteralPath $output) { Remove-Item -LiteralPath $output -Recurse -Force }
}
Invoke-Unity 'build' @('-quit','-executeMethod','Game.Editor.ClientBuild.Build')
& python "$PSScriptRoot/package_release.py" Builds/Client Builds/Download $revision
if ($LASTEXITCODE -ne 0) { throw 'Windows packaging failed.' }
