$ErrorActionPreference = 'Stop'
$record = Join-Path (Get-Location).Path 'Logs/windows-unity-process.json'
if (!(Test-Path -LiteralPath $record)) { return }
$owned = Get-Content -LiteralPath $record -Raw | ConvertFrom-Json
$process = Get-Process -Id $owned.Id -ErrorAction SilentlyContinue
# PID may have been recycled: require exact start time and executable identity.
if ($process -and $process.StartTime.ToUniversalTime().Ticks.ToString() -eq $owned.Started -and $process.Path -eq $owned.Path) {
    & taskkill.exe /PID $owned.Id /T /F | Out-Null
    if ($LASTEXITCODE -ne 0 -and (Get-Process -Id $owned.Id -ErrorAction SilentlyContinue)) {
        throw 'Could not stop this build process tree.'
    }
}
Remove-Item -LiteralPath $record -Force
