$ErrorActionPreference = 'Stop'
$record = Join-Path (Get-Location).Path 'Logs/windows-unity-process.json'
if (!(Test-Path -LiteralPath $record)) { return }
$owned = Get-Content -LiteralPath $record -Raw | ConvertFrom-Json
$process = Get-Process -Id $owned.Id -ErrorAction SilentlyContinue
# PID may have been recycled: require exact start time and executable identity.
if ($process -and $process.StartTime.ToUniversalTime().Ticks.ToString() -eq $owned.Started -and $process.Path -eq $owned.Path) {
    $null = $process.Handle
    & taskkill.exe /PID $owned.Id /T /F | Out-Null
    if (!$process.WaitForExit(10000)) {
        throw 'Build process did not exit after the stop request.'
    }
}
Remove-Item -LiteralPath $record -Force
