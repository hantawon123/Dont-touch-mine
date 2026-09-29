param(
 [Parameter(Mandatory)][string]$PlayerPath,
 [Parameter(Mandatory)][string]$RuntimeRoot,
 [Parameter(Mandatory)][string]$OutputRoot,
 [Parameter(Mandatory)][ValidatePattern('^[A-Za-z0-9][A-Za-z0-9_.-]{0,31}$')][string]$Run,
 [ValidateSet('Host','Server')][string]$Mode='Host',
 [ValidateRange(2,6)][int]$Count=6,
 [ValidateRange(60,1800)][int]$Seconds=290
)
$ErrorActionPreference='Stop'
$PlayerPath=(Resolve-Path -LiteralPath $PlayerPath).Path
$OutputRoot=[IO.Path]::GetFullPath($OutputRoot)
$out=Join-Path $OutputRoot $Run
if(Test-Path -LiteralPath $out){throw "Run directory already exists: $out"}
$credentials=Get-Content -LiteralPath (Join-Path $RuntimeRoot 'credentials.json') -Raw | ConvertFrom-Json
if([string]::IsNullOrWhiteSpace($credentials.token)){throw 'Missing collector token'}
$previousMetrics=@{}
foreach($name in @('GAME_METRICS_URL','GAME_METRICS_TOKEN','GAME_METRICS_BUILD')){$previousMetrics[$name]=[Environment]::GetEnvironmentVariable($name,'Process')}
$players=@()
try {
 $env:GAME_METRICS_URL='http://127.0.0.1:9464/ingest'
 $env:GAME_METRICS_TOKEN=$credentials.token
 $env:GAME_METRICS_BUILD=$Run
 New-Item -ItemType Directory -Path $out | Out-Null
 Write-Output "Grafana telemetry enabled: $Run ($Mode)"
 $room='B'+[Guid]::NewGuid().ToString('N').Substring(0,10)
 $processCount=if($Mode -eq 'Server'){$Count+1}else{$Count}
 for($i=0;$i -lt $processCount;$i++) {
  $argsList=@('--stats-mode',$Mode,'--stats-room',$room,'--stats-peer',"$i",'--stats-count',"$Count",'--stats-seconds',"$Seconds",'--stats-output',('"'+$out+"/peer$i"+'"'),'-logFile',('"'+$out+"/peer$i.log"+'"'),'-screen-width','1280','-screen-height','720','-screen-fullscreen','0')
  if($i -ne 1){$argsList+=@('-batchmode','-nographics')}
  $p=Start-Process -FilePath $PlayerPath -ArgumentList $argsList -WindowStyle Hidden -PassThru
  $players+=$p
  Write-Output "Started peer $i PID $($p.Id)"
  if($i -eq 0){Start-Sleep -Seconds 8}
 }
 $peerStarts=@($players | ForEach-Object { @{pid=$_.Id;utc=$_.StartTime.ToUniversalTime().ToString('o')} })
 @{room=$room;players=$players.Id;count=$Count;seconds=$Seconds;graphicsPeer=1;resolution='1280x720';fpsCap=60;mode=$Mode;processCount=$processCount;executable=$PlayerPath;peerStarts=$peerStarts;started=(Get-Date).ToString('o')} | ConvertTo-Json -Depth 5 | Set-Content ($out+'/manifest.json')
 'utc,pid,cpu_seconds,working_set_bytes' | Set-Content ($out+'/process.csv')
 $until=(Get-Date).AddSeconds($Seconds+40)
 while((Get-Date) -lt $until){
  $alive=0
  foreach($p in $players){$p.Refresh();if(!$p.HasExited){$alive++;"$((Get-Date).ToString('o')),$($p.Id),$($p.TotalProcessorTime.TotalSeconds),$($p.WorkingSet64)" | Add-Content ($out+'/process.csv')}}
  if($alive -eq 0){break}
  Start-Sleep -Seconds 1
 }
 foreach($p in $players){$p.Refresh();if(!$p.HasExited){$p.Kill();Write-Output "Timed out own peer $($p.Id)"}}
 Write-Output "Run ended: $Run"
} finally {
 try {
  foreach($p in $players){$p.Refresh();if(!$p.HasExited){$p.Kill()}}
 } finally {
  foreach($name in $previousMetrics.Keys){[Environment]::SetEnvironmentVariable($name,$previousMetrics[$name],'Process')}
 }
}
