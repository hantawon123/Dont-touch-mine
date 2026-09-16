$ErrorActionPreference = 'Stop'
foreach ($file in @('build-windows.ps1','stop-windows-build.ps1')) {
    $parseErrors=$null; $tokens=$null
    [Management.Automation.Language.Parser]::ParseFile((Join-Path $PSScriptRoot $file),[ref]$tokens,[ref]$parseErrors) | Out-Null
    if ($parseErrors.Count) { throw ($parseErrors | Out-String) }
}
$fixture = Join-Path ([IO.Path]::GetTempPath()) ('d205-stop-' + [guid]::NewGuid())
New-Item -ItemType Directory "$fixture/Logs" -Force | Out-Null
$process=$null
Push-Location $fixture
try {
    $exe=(Get-Process -Id $PID).Path
    $process=Start-Process -FilePath $exe -ArgumentList '-NoProfile -Command Start-Sleep -Seconds 60' -WindowStyle Hidden -PassThru
    $null=$process.Handle
    # A recycled PID must not stop an unrelated process.
    @{ Id=$process.Id; Started='0'; Path=$exe } | ConvertTo-Json | Set-Content Logs/windows-unity-process.json
    & "$PSScriptRoot/stop-windows-build.ps1"
    if (!(Get-Process -Id $process.Id -ErrorAction SilentlyContinue)) { throw 'Unowned process was stopped.' }
    @{ Id=$process.Id; Started=$process.StartTime.ToUniversalTime().Ticks.ToString(); Path=$exe } | ConvertTo-Json | Set-Content Logs/windows-unity-process.json
    & "$PSScriptRoot/stop-windows-build.ps1"
    if (!$process.WaitForExit(10000)) { throw 'Owned process survived cleanup.' }
    Write-Output 'PASS: PowerShell syntax, PID reuse guard, owned process cleanup'
} finally {
    if ($process -and (Get-Process -Id $process.Id -ErrorAction SilentlyContinue)) { Stop-Process -Id $process.Id -Force }
    Pop-Location
    $resolved=[IO.Path]::GetFullPath($fixture)
    if (!$resolved.StartsWith([IO.Path]::GetFullPath([IO.Path]::GetTempPath()),[StringComparison]::OrdinalIgnoreCase)) { throw 'Unexpected fixture path.' }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
