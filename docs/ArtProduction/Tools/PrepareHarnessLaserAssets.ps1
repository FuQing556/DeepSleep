param(
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
)

$sourceRoot = Join-Path $ProjectRoot 'docs\ArtProduction'
$characterOutputRoot = Join-Path $ProjectRoot 'Assets\_Project\Art\Characters\Harness'
$laserOutputRoot = Join-Path $ProjectRoot 'Assets\_Project\Art\VFX\Harness\Laser'

New-Item -ItemType Directory -Force -Path $characterOutputRoot | Out-Null
New-Item -ItemType Directory -Force -Path $laserOutputRoot | Out-Null

Add-Type -AssemblyName System.Drawing.Common

$runtimeDirectory = Split-Path -Parent ([System.Object].Assembly.Location)
$source = @'
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

public static class HarnessLaserAssetProcessor
{
    private static Bitmap CreateCanvas(int width, int height)
    {
        var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        bitmap.SetResolution(96f, 96f);
        return bitmap;
    }

    private static Graphics CreateGraphics(Bitmap destination)
    {
        Graphics graphics = Graphics.FromImage(destination);
        graphics.Clear(Color.Transparent);
        graphics.CompositingMode = CompositingMode.SourceCopy;
        graphics.CompositingQuality = CompositingQuality.HighQuality;
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.SmoothingMode = SmoothingMode.HighQuality;
        return graphics;
    }

    public static void ResizeCanvas(string inputPath, string outputPath, int width, int height)
    {
        using (var source = new Bitmap(inputPath))
        using (var destination = CreateCanvas(width, height))
        using (Graphics graphics = CreateGraphics(destination))
        {
            graphics.DrawImage(source, new Rectangle(0, 0, width, height));
            destination.Save(outputPath, ImageFormat.Png);
        }
    }

    public static void CreateBeamStrip(
        string inputPath,
        string outputPath,
        int width,
        int height,
        int verticalPadding,
        bool forceHeight)
    {
        using (var source = new Bitmap(inputPath))
        {
            int minimumY = source.Height;
            int maximumY = -1;

            for (int y = 0; y < source.Height; y++)
            {
                for (int x = 0; x < source.Width; x++)
                {
                    if (source.GetPixel(x, y).A <= 4)
                    {
                        continue;
                    }

                    minimumY = Math.Min(minimumY, y);
                    maximumY = Math.Max(maximumY, y);
                }
            }

            if (maximumY < minimumY)
            {
                throw new InvalidOperationException("The source texture has no visible pixels: " + inputPath);
            }

            minimumY = Math.Max(0, minimumY - verticalPadding);
            maximumY = Math.Min(source.Height - 1, maximumY + verticalPadding);
            int sourceHeight = maximumY - minimumY + 1;
            float horizontalScale = (float)width / source.Width;
            int drawnHeight = forceHeight
                ? height
                : Math.Min(height, Math.Max(1, (int)Math.Round(sourceHeight * horizontalScale)));
            int destinationY = (height - drawnHeight) / 2;

            using (var destination = CreateCanvas(width, height))
            using (Graphics graphics = CreateGraphics(destination))
            {
                graphics.DrawImage(
                    source,
                    new Rectangle(0, destinationY, width, drawnHeight),
                    new Rectangle(0, minimumY, source.Width, sourceHeight),
                    GraphicsUnit.Pixel);

                // Repeat wrapping samples both sides of the texture. Making the final
                // column identical to the first prevents a one-pixel seam at UV 0/1.
                for (int y = 0; y < height; y++)
                {
                    destination.SetPixel(width - 1, y, destination.GetPixel(0, y));
                }

                destination.Save(outputPath, ImageFormat.Png);
            }
        }
    }
}
'@

$referenceAssemblies = @(
    [System.Drawing.Bitmap].Assembly.Location,
    (Join-Path $runtimeDirectory 'System.Drawing.Primitives.dll'),
    (Join-Path $runtimeDirectory 'System.Collections.dll'),
    (Join-Path $runtimeDirectory 'System.Runtime.dll'),
    (Join-Path $runtimeDirectory 'System.Private.CoreLib.dll'),
    (Join-Path $runtimeDirectory 'System.Private.Windows.Core.dll'),
    (Join-Path $runtimeDirectory 'System.Private.Windows.GdiPlus.dll')
)

Add-Type -TypeDefinition $source -ReferencedAssemblies $referenceAssemblies

[HarnessLaserAssetProcessor]::ResizeCanvas(
    (Join-Path $sourceRoot 'Characters\Harness\CANDIDATE_HA_LaserFireFly_v02_ALPHA.png'),
    (Join-Path $characterOutputRoot 'SPR_HA_LaserFire_v01.png'),
    1280,
    1280)

[HarnessLaserAssetProcessor]::ResizeCanvas(
    (Join-Path $sourceRoot 'VFX\Harness\Laser\CANDIDATE_VFX_HA_LockReticle_v01.png'),
    (Join-Path $laserOutputRoot 'VFX_HA_TargetReticle_v01.png'),
    512,
    512)

[HarnessLaserAssetProcessor]::CreateBeamStrip(
    (Join-Path $sourceRoot 'VFX\Harness\Laser\CANDIDATE_VFX_HA_LaserBeamBody_v01.png'),
    (Join-Path $laserOutputRoot 'TEX_HA_LaserBeamBody_v01.png'),
    1024,
    256,
    16,
    $false)

[HarnessLaserAssetProcessor]::ResizeCanvas(
    (Join-Path $sourceRoot 'VFX\Harness\Laser\CANDIDATE_VFX_HA_LaserMuzzle_v01.png'),
    (Join-Path $laserOutputRoot 'VFX_HA_LaserMuzzle_v01.png'),
    512,
    512)

[HarnessLaserAssetProcessor]::ResizeCanvas(
    (Join-Path $sourceRoot 'VFX\Harness\Laser\CANDIDATE_VFX_HA_LaserHit_v01.png'),
    (Join-Path $laserOutputRoot 'VFX_HA_LaserHit_v01.png'),
    512,
    512)

[HarnessLaserAssetProcessor]::CreateBeamStrip(
    (Join-Path $sourceRoot 'VFX\Harness\Laser\CANDIDATE_TEX_HA_LaserOverclock_v02.png'),
    (Join-Path $laserOutputRoot 'TEX_HA_LaserOverclock_v01.png'),
    1024,
    256,
    8,
    $true)

[HarnessLaserAssetProcessor]::CreateBeamStrip(
    (Join-Path $sourceRoot 'VFX\Harness\Laser\CANDIDATE_TEX_HA_LaserOverclock_v01.png'),
    (Join-Path $laserOutputRoot 'TEX_HA_LaserSurgeFrame_v01.png'),
    1024,
    512,
    8,
    $false)

$outputs = @(
    (Join-Path $characterOutputRoot 'SPR_HA_LaserFire_v01.png')
    (Join-Path $laserOutputRoot 'VFX_HA_TargetReticle_v01.png')
    (Join-Path $laserOutputRoot 'TEX_HA_LaserBeamBody_v01.png')
    (Join-Path $laserOutputRoot 'VFX_HA_LaserMuzzle_v01.png')
    (Join-Path $laserOutputRoot 'VFX_HA_LaserHit_v01.png')
    (Join-Path $laserOutputRoot 'TEX_HA_LaserOverclock_v01.png')
    (Join-Path $laserOutputRoot 'TEX_HA_LaserSurgeFrame_v01.png')
)

Get-Item -LiteralPath $outputs |
    Select-Object FullName, Length
