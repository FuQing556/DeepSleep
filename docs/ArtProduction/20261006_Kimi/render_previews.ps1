# Offline art QA only: composes original generated PNGs; does not modify source assets.
Add-Type -AssemblyName System.Drawing
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$cache = @{}
function Asset([string]$name) {
    if (!$cache.ContainsKey($name)) {
        $path = if ([IO.Path]::IsPathRooted($name)) { $name } else { Join-Path "$root/raw" $name }
        $cache[$name] = [Drawing.Bitmap]::new($path)
    }
    return $cache[$name]
}
function Fit($g, [string]$name, [float]$x, [float]$y, [float]$w, [float]$h) {
    $im = Asset $name
    $scale = [Math]::Min($w/$im.Width, $h/$im.Height)
    $g.DrawImage($im, [Drawing.RectangleF]::new($x+($w-$im.Width*$scale)/2, $y+($h-$im.Height*$scale)/2, $im.Width*$scale, $im.Height*$scale))
}
$font = [Drawing.Font]::new('Microsoft YaHei', 16)
$small = [Drawing.Font]::new('Microsoft YaHei', 11)
$white = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(240,240,250))
try {
    $poses = @('Idle','FluteCharge','MoonCast','PrismCast','LaserCharge','LaserRelease','TideRelease','Stagger','PhaseChange','Bow')
    $sheet = [Drawing.Bitmap]::new(1600,760)
    $g = [Drawing.Graphics]::FromImage($sheet)
    $g.Clear([Drawing.Color]::FromArgb(24,30,49))
    $g.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    for($i=0;$i -lt $poses.Count;$i++) {
        $x=($i%5)*320; $y=[Math]::Floor($i/5)*380
        Fit $g ('SPR_KI_'+$poses[$i]+'_v01.png') $x ($y+12) 320 320
        $g.DrawString($poses[$i],$font,$white,[single]($x+20),[single]($y+342))
    }
    $sheet.Save("$root/previews/character_actions.png",[Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose();$sheet.Dispose()

    $base = (Resolve-Path "$root/../../..").Path
    $refSheet=[Drawing.Bitmap]::new(1200,450);$g=[Drawing.Graphics]::FromImage($refSheet)
    $g.Clear([Drawing.Color]::FromArgb(24,30,49));$g.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $refs=@("$base/Assets/_Project/Art/Characters/DeepSeek/SPR_DS_Idle_Base_v01.png","$base/Assets/_Project/Art/Characters/Harness/SPR_HA_IdleFly_v03.png","$base/Assets/_Project/Art/Characters/Doubao/SPR_DB_RooftopExplain_v01.png",'SPR_KI_Idle_v01.png')
    $labels=@('DS / authored scale','HS / authored scale','Doubao / authored scale','Kimi / 512 PPU proposal')
    for($i=0;$i -lt 4;$i++){Fit $g $refs[$i] ($i*300) 40 300 300;$g.DrawString($labels[$i],$small,$white,[single]($i*300+12),[single]370)}
    $refSheet.Save("$root/previews/scale_reference.png",[Drawing.Imaging.ImageFormat]::Png);$g.Dispose();$refSheet.Dispose()

    foreach($variant in @('idle','blades')) {
        $canvas=[Drawing.Bitmap]::new(1280,582);$g=[Drawing.Graphics]::FromImage($canvas)
        $g.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $bg=Asset 'BG_W01_MoonRiver_Panorama_v01.png';$scale=582/$bg.Height
        $g.DrawImage($bg,[Drawing.RectangleF]::new((1280-$bg.Width*$scale)/2,0,$bg.Width*$scale,582))
        Fit $g 'SPR_KI_CloudPlatform_v01.png' 876 330 185 65
        Fit $g 'SPR_KI_Idle_v01.png' 893 212 150 150
        Fit $g $refs[0] 283 230 145 145
        Fit $g $refs[1] 431 311 145 145
        if($variant -eq 'blades'){
            Fit $g 'VFX_KI_MoonBlade_v01.png' 590 115 170 68
            Fit $g 'VFX_KI_TidalBlade_v02.png' 685 222 150 240
            Fit $g 'VFX_KI_PrismOrb_v01.png' 544 196 35 35
        }
        $g.DrawString('ART SCALE PREVIEW - not gameplay / phone capture',$small,$white,[single]18,[single]552)
        $canvas.Save("$root/previews/mobile_aspect_$variant.png",[Drawing.Imaging.ImageFormat]::Png);$g.Dispose();$canvas.Dispose()
    }
    $hud=[Drawing.Bitmap]::new(1280,420);$g=[Drawing.Graphics]::FromImage($hud)
    $g.Clear([Drawing.Color]::FromArgb(24,30,49));$g.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    # Cropped source rectangles from measured alpha bounds. Preview only, raw PNGs unchanged.
    $frame=Asset 'UI_KI_HealthFrame_v01.png';$back=Asset 'UI_KI_HealthBack_v01.png';$fill=Asset 'UI_KI_HealthFill_v01.png'
    $counter=Asset 'UI_KI_ShieldCounterFrame_v01.png'
    foreach($row in @(0,1)) {
        $top=40+$row*180
        $slotState=$g.Save();$g.SetClip([Drawing.RectangleF]::new(349,$top+44,583,20))
        $g.DrawImage($back,[Drawing.RectangleF]::new(349,$top+37,583,34.5),[Drawing.RectangleF]::new(25,451,1536,91),[Drawing.GraphicsUnit]::Pixel)
        $state=$g.Save();$g.SetClip([Drawing.RectangleF]::new(349,$top+44,(583*(1-$row*.5)),20))
        $g.DrawImage($fill,[Drawing.RectangleF]::new(349,$top+37,583,33.6),[Drawing.RectangleF]::new(39,450,1508,87),[Drawing.GraphicsUnit]::Pixel);$g.Restore($state);$g.Restore($slotState)
        $g.DrawImage($frame,[Drawing.RectangleF]::new(280,$top,720,78.34),[Drawing.RectangleF]::new(25,216,2123,231),[Drawing.GraphicsUnit]::Pixel)
        $g.DrawString($(if($row -eq 0){'Kimi  10000 / 10000'}else{'Kimi  5000 / 10000'}),$font,$white,[single]510,[single]($top+83))
        $g.DrawImage($counter,[Drawing.RectangleF]::new(505,$top+111,270,62.18),[Drawing.RectangleF]::new(100,124,1967,453),[Drawing.GraphicsUnit]::Pixel)
        $g.DrawString($(if($row -eq 0){'100 hits'}else{'200 hits'}),$font,$white,[single]595,[single]($top+126))
    }
    $g.DrawString('ART LAYER PREVIEW - not runtime uGUI',$small,$white,[single]18,[single]397)
    $hud.Save("$root/previews/hud_layers.png",[Drawing.Imaging.ImageFormat]::Png);$g.Dispose();$hud.Dispose()
} finally {
    foreach($im in $cache.Values){$im.Dispose()}
    $font.Dispose();$small.Dispose();$white.Dispose()
}
