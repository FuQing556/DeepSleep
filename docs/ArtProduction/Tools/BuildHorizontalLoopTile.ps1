param(
    [Parameter(Mandatory = $true)]
    [string] $InputPath,

    [Parameter(Mandatory = $true)]
    [string] $OutputPath,

    [Parameter(Mandatory = $true)]
    [string] $PreviewPath,

    [int] $Width = 2048,
    [int] $Height = 1080,
    [int] $EdgeBlendPixels = 192,
    [int] $ExactBandPixels = 16
)

$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Drawing.Common

if (-not ('DeepSleep.Art.HorizontalLoopTileBuilder' -as [type])) {
    $drawingAssemblies = @(
        [System.Drawing.Bitmap].Assembly.Location,
        [System.Drawing.Color].Assembly.Location,
        (Join-Path $PSHOME 'System.Private.Windows.GdiPlus.dll'),
        (Join-Path $PSHOME 'System.Private.Windows.Core.dll')
    ) | Select-Object -Unique
    Add-Type -ReferencedAssemblies $drawingAssemblies -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace DeepSleep.Art
{
    public static class HorizontalLoopTileBuilder
    {
        public static void Build(
            string inputPath,
            string outputPath,
            string previewPath,
            int width,
            int height,
            int edgeBlendPixels,
            int exactBandPixels)
        {
            if (width <= 0 || height <= 0)
                throw new ArgumentOutOfRangeException("Output dimensions must be positive.");
            if (edgeBlendPixels <= 1 || edgeBlendPixels * 2 >= width)
                throw new ArgumentOutOfRangeException(nameof(edgeBlendPixels));
            if (exactBandPixels < 1 || exactBandPixels >= edgeBlendPixels)
                throw new ArgumentOutOfRangeException(nameof(exactBandPixels));

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            Directory.CreateDirectory(Path.GetDirectoryName(previewPath));

            using (var source = new Bitmap(inputPath))
            using (var tile = new Bitmap(width, height, PixelFormat.Format24bppRgb))
            {
                DrawCover(source, tile);
                BlendHorizontalSeam(tile, edgeBlendPixels, exactBandPixels);
                tile.Save(outputPath, ImageFormat.Png);

                using (var preview = new Bitmap(width * 2, height, PixelFormat.Format24bppRgb))
                using (var graphics = Graphics.FromImage(preview))
                {
                    graphics.DrawImageUnscaled(tile, 0, 0);
                    graphics.DrawImageUnscaled(tile, width, 0);
                    preview.Save(previewPath, ImageFormat.Png);
                }
            }
        }

        private static void DrawCover(Bitmap source, Bitmap destination)
        {
            var scale = Math.Max(
                destination.Width / (double)source.Width,
                destination.Height / (double)source.Height);
            var drawWidth = (int)Math.Ceiling(source.Width * scale);
            var drawHeight = (int)Math.Ceiling(source.Height * scale);
            var drawX = (destination.Width - drawWidth) / 2;
            var drawY = (destination.Height - drawHeight) / 2;

            using (var graphics = Graphics.FromImage(destination))
            {
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.DrawImage(source, drawX, drawY, drawWidth, drawHeight);
            }
        }

        private static void BlendHorizontalSeam(Bitmap tile, int blendWidth, int exactBand)
        {
            const int sampleWidth = 16;

            for (var y = 0; y < tile.Height; y++)
            {
                long red = 0;
                long green = 0;
                long blue = 0;

                for (var sample = 0; sample < sampleWidth; sample++)
                {
                    var left = tile.GetPixel(blendWidth + sample, y);
                    var right = tile.GetPixel(tile.Width - blendWidth - sample - 1, y);
                    red += left.R + right.R;
                    green += left.G + right.G;
                    blue += left.B + right.B;
                }

                var divisor = sampleWidth * 2;
                var seam = Color.FromArgb(
                    (int)(red / divisor),
                    (int)(green / divisor),
                    (int)(blue / divisor));

                for (var offset = 0; offset < blendWidth; offset++)
                {
                    var t = offset < exactBand
                        ? 0.0
                        : (offset - exactBand) / (double)(blendWidth - exactBand - 1);
                    t = t * t * (3.0 - (2.0 * t));

                    var leftX = offset;
                    var rightX = tile.Width - offset - 1;
                    var originalLeft = tile.GetPixel(leftX, y);
                    var originalRight = tile.GetPixel(rightX, y);

                    tile.SetPixel(leftX, y, Blend(seam, originalLeft, t));
                    tile.SetPixel(rightX, y, Blend(seam, originalRight, t));
                }
            }
        }

        private static Color Blend(Color from, Color to, double t)
        {
            return Color.FromArgb(
                LerpByte(from.R, to.R, t),
                LerpByte(from.G, to.G, t),
                LerpByte(from.B, to.B, t));
        }

        private static int LerpByte(byte from, byte to, double t)
        {
            return Math.Max(0, Math.Min(255, (int)Math.Round(from + ((to - from) * t))));
        }
    }
}
'@
}

$resolvedInput = (Resolve-Path -LiteralPath $InputPath).Path
$resolvedOutput = [System.IO.Path]::GetFullPath($OutputPath)
$resolvedPreview = [System.IO.Path]::GetFullPath($PreviewPath)

[DeepSleep.Art.HorizontalLoopTileBuilder]::Build(
    $resolvedInput,
    $resolvedOutput,
    $resolvedPreview,
    $Width,
    $Height,
    $EdgeBlendPixels,
    $ExactBandPixels)

Write-Output "Built: $resolvedOutput"
Write-Output "Preview: $resolvedPreview"
