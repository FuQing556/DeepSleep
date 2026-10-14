Add-Type -AssemblyName System.Drawing
$project = 'D:\Unity Work\DeepSleep_Unity6'
$output = Join-Path $project 'docs/ArtProduction/20261012_PlayerActions/previews/scale_audit'
New-Item -ItemType Directory -Force -Path $output | Out-Null
$all = Get-ChildItem -LiteralPath (Join-Path $project 'Assets/_Project/Art/Characters') -Filter 'SPR_*.png' -Recurse
$groups = @{
    DS = @($all | Where-Object { $_.Name -like 'SPR_DS_*' -and $_.Name -notlike '*BorrowedBadge*' })
    HS = @($all | Where-Object { $_.Name -like 'SPR_HA_*' })
    Badge = @($all | Where-Object { $_.Name -like '*BorrowedBadge*' })
    Kimi = @($all | Where-Object { $_.Name -like 'SPR_KI_*' -and $_.Name -notlike '*CloudPlatform*' })
    Claude = @($all | Where-Object { $_.Name -like 'SPR_CL_*' -and $_.Name -notlike '*HoverDrone*' -and $_.Name -notlike '*PermissionBook*' })
}
$font = New-Object System.Drawing.Font('Arial',10)
$records = @()
foreach($key in $groups.Keys) {
    $files = @($groups[$key] | Sort-Object Name)
    $rows = [int][Math]::Ceiling($files.Count / 4.0)
    $sheet = New-Object System.Drawing.Bitmap(1200,($rows*310))
    $graphics = [System.Drawing.Graphics]::FromImage($sheet)
    $graphics.Clear([System.Drawing.Color]::FromArgb(42,47,60))
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    for($i=0;$i -lt $files.Count;$i++) {
        $file = $files[$i]
        $meta = Get-Content -LiteralPath ($file.FullName+'.meta') -Raw
        $ppu = [double]([regex]::Match($meta,'spritePixelsToUnits:\s*([\d.]+)').Groups[1].Value)
        $img = [System.Drawing.Image]::FromFile($file.FullName)
        $w = [single]($img.Width / $ppu * 90); $h = [single]($img.Height / $ppu * 90)
        $x = ($i%4)*300; $y = [int][Math]::Floor($i/4)*310
        $graphics.DrawRectangle([System.Drawing.Pens]::DimGray,$x+35,$y+10,230,230)
        $graphics.DrawImage($img,[single]($x+150-$w/2),[single]($y+125-$h/2),$w,$h)
        $name = $file.BaseName.Replace('SPR_','').Replace('BorrowedBadge','Badge')
        $graphics.DrawString($name,$font,[System.Drawing.Brushes]::White,[single]($x+5),[single]($y+248))
        $graphics.DrawString(('PPU {0}; {1} x {2}' -f $ppu,$img.Width,$img.Height),$font,[System.Drawing.Brushes]::LightGray,[single]($x+5),[single]($y+270))
        $records += [pscustomobject]@{Group=$key;File=$file.FullName;Width=$img.Width;Height=$img.Height;PPU=$ppu}
        $img.Dispose()
    }
    $sheet.Save((Join-Path $output ($key+'.png')),[System.Drawing.Imaging.ImageFormat]::Png)
    $graphics.Dispose(); $sheet.Dispose()
}
$font.Dispose()
$records | Export-Csv -LiteralPath (Join-Path $output 'source_sizes.csv') -NoTypeInformation -Encoding utf8
Write-Output $output
