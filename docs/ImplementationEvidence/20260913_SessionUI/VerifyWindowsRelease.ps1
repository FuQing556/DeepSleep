param([string]$Version = '1.1.0')
$ErrorActionPreference = 'Stop'
$project = 'D:\Unity Work\DeepSleep_Unity6'
$evidence = Join-Path $project 'docs\ImplementationEvidence\20260913_SessionUI'
$exe = Join-Path $project "Releases\v$Version\Windows\ICanFly.exe"
Add-Type -AssemblyName System.Drawing
$icon = [System.Drawing.Icon]::ExtractAssociatedIcon($exe)
$bitmap = $icon.ToBitmap()
$bitmap.Save((Join-Path $evidence "windows-exe-icon-$Version.png"), [System.Drawing.Imaging.ImageFormat]::Png)
$bitmap.Dispose()
$icon.Dispose()
$logPath = Join-Path $evidence "windows-release-smoke-$Version.log"
$process = Start-Process -FilePath $exe -ArgumentList @('-batchmode','-nographics','-logFile',('"' + $logPath + '"')) -WindowStyle Hidden -PassThru
try {
    Start-Sleep -Seconds 12
    $process.Refresh()
    Write-Output "Alive after startup: $(!$process.HasExited); PID=$($process.Id)"
    if (Test-Path -LiteralPath $logPath) { Get-Content -LiteralPath $logPath -Tail 45 }
} finally {
    if (!$process.HasExited) { Stop-Process -Id $process.Id }
}
