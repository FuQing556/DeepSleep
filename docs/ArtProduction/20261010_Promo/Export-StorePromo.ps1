Add-Type -AssemblyName System.Drawing
$sourcePath = Join-Path $PSScriptRoot 'raw/ChinaAI_Promo_Landscape_v02.png'
$outputPath = Join-Path $PSScriptRoot 'ChinaAI_Promo_1920x1080.jpg'
$sourceImage = [System.Drawing.Image]::FromFile($sourcePath)
$outputImage = [System.Drawing.Bitmap]::new(1920,1080)
$graphics = [System.Drawing.Graphics]::FromImage($outputImage)
try {
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $scale = [Math]::Max(1920.0 / $sourceImage.Width,1080.0 / $sourceImage.Height)
    $width = [single]($sourceImage.Width * $scale)
    $height = [single]($sourceImage.Height * $scale)
    $graphics.DrawImage($sourceImage,[single]((1920-$width)/2),[single]((1080-$height)/2),$width,$height)
    $encoder = [System.Drawing.Imaging.ImageCodecInfo]::GetImageEncoders() | Where-Object MimeType -eq 'image/jpeg'
    $parameters = [System.Drawing.Imaging.EncoderParameters]::new(1)
    try {
        $parameters.Param[0] = [System.Drawing.Imaging.EncoderParameter]::new([System.Drawing.Imaging.Encoder]::Quality,[long]95)
        $outputImage.Save($outputPath,$encoder,$parameters)
    } finally { $parameters.Dispose() }
} finally {
    $graphics.Dispose()
    $outputImage.Dispose()
    $sourceImage.Dispose()
}
$check = [System.Drawing.Image]::FromFile($outputPath)
try {
    $file = Get-Item -LiteralPath $outputPath
    if ($check.Width -ne 1920 -or $check.Height -ne 1080 -or $file.Length -ge 4000000) { throw 'Store size verification failed.' }
    Write-Output "$outputPath : $($check.Width)x$($check.Height), $($file.Length) bytes"
} finally { $check.Dispose() }
