param(
    [Parameter(Mandatory=$true)][string]$Apk,
    [Parameter(Mandatory=$true)][string]$Version,
    [Parameter(Mandatory=$true)][int]$VersionCode
)
$ErrorActionPreference = 'Stop'
$aapt = 'D:/银花符青/Unity Editor/6000.6.0f1/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/build-tools/36.0.0/aapt.exe'
$Apk = (Resolve-Path -LiteralPath $Apk).Path
$badging = (& $aapt dump badging $Apk) -join "`n"
if ($LASTEXITCODE -ne 0) { throw 'Cannot inspect APK' }
$manifest = (& $aapt dump xmltree $Apk AndroidManifest.xml) -join "`n"
if ($LASTEXITCODE -ne 0) { throw 'Cannot inspect manifest' }
if (!$badging.Contains("name='com.DefaultCompany.DeepSleep_Unity6'")) { throw 'Package ID changed' }
if (!$badging.Contains("versionName='$Version'")) { throw 'Incorrect version name' }
if (!$badging.Contains("versionCode='$VersionCode'")) { throw 'Incorrect version code' }
if (!$badging.Contains("application-label:'中国AI会飞'")) { throw 'Incorrect application name' }
# Android ActivityInfo.SCREEN_ORIENTATION_LANDSCAPE = 0, not FULL_USER = 13.
$directions = @($manifest -split "`n" | Where-Object { $_ -match 'android:screenOrientation' })
if ($directions.Count -ne 1 -or $directions[0] -notmatch '\(type 0x10\)0x0\s*$') {
    throw "APK is not fixed landscape: $($directions -join '; ')"
}
foreach ($permission in @('INTERNET','ACCESS_WIFI_STATE','CHANGE_WIFI_MULTICAST_STATE')) {
    if (!$badging.Contains("android.permission.$permission")) { throw "Missing permission: $permission" }
}
Write-Output "PASS: $Version/code$VersionCode; original package ID; Chinese name; landscape (0); LAN permissions."
Write-Output $directions[0].Trim()
