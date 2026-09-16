param([Parameter(Mandatory=$true)][string]$Directory)
Get-ChildItem -LiteralPath $Directory -Filter 'trial-*.csv' | Sort-Object Name | ForEach-Object {
    $rows = @(Import-Csv -LiteralPath $_.FullName)
    $times = @($rows | ForEach-Object {[double]::Parse($_.ms,[Globalization.CultureInfo]::InvariantCulture)} | Sort-Object)
    $sum = ($times | Measure-Object -Sum).Sum
    [pscustomobject]@{
        trial=$_.BaseName
        fps=[Math]::Round(1000*$times.Count/$sum,2)
        p95=[Math]::Round($times[[Math]::Ceiling($times.Count*0.95)-1],2)
        p99=[Math]::Round($times[[Math]::Ceiling($times.Count*0.99)-1],2)
        batches=[Math]::Round(($rows | Measure-Object batches -Average).Average,0)
        triangles=[Math]::Round(($rows | Measure-Object triangles -Average).Average,0)
    }
}