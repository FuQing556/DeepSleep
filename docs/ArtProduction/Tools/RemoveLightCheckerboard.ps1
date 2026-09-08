param(
    [Parameter(Mandatory = $true)]
    [string]$InputPath,

    [Parameter(Mandatory = $true)]
    [string]$OutputPath,

    [string[]]$ExtraSeed = @()
)

$source = @'
using System;
using System.Drawing;
using System.Drawing.Imaging;

public static class LightCheckerboardRemover
{
    public static void Process(string inputPath, string outputPath, string extraSeeds)
    {
        using (var source = new Bitmap(inputPath))
        using (var output = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb))
        {
            int width = source.Width;
            int height = source.Height;
            var background = new bool[width * height];
            var queue = new int[width * height];
            int queueHead = 0;
            int queueTail = 0;

            Action<int, int> enqueueIfBackground = (x, y) =>
            {
                int index = y * width + x;
                if (background[index])
                {
                    return;
                }

                Color color = source.GetPixel(x, y);
                int maximum = Math.Max(color.R, Math.Max(color.G, color.B));
                int minimum = Math.Min(color.R, Math.Min(color.G, color.B));

                // Image generation sometimes paints a white/light-gray checkerboard
                // instead of writing an alpha channel. Only flood through the light,
                // nearly neutral pixels connected to the canvas edge so that white
                // costume areas enclosed by the character outline remain intact.
                if (minimum >= 212 && maximum - minimum <= 18)
                {
                    background[index] = true;
                    queue[queueTail++] = index;
                }
            };

            for (int x = 0; x < width; x++)
            {
                enqueueIfBackground(x, 0);
                enqueueIfBackground(x, height - 1);
            }

            for (int y = 0; y < height; y++)
            {
                enqueueIfBackground(0, y);
                enqueueIfBackground(width - 1, y);
            }

            if (!String.IsNullOrWhiteSpace(extraSeeds))
            {
                foreach (string seed in extraSeeds.Split(';'))
                {
                    string[] coordinates = seed.Split(',');
                    int x;
                    int y;
                    if (coordinates.Length == 2 &&
                        Int32.TryParse(coordinates[0], out x) &&
                        Int32.TryParse(coordinates[1], out y) &&
                        x >= 0 && x < width && y >= 0 && y < height)
                    {
                        enqueueIfBackground(x, y);
                    }
                }
            }

            while (queueHead < queueTail)
            {
                int index = queue[queueHead++];
                int x = index % width;
                int y = index / width;

                if (x > 0) enqueueIfBackground(x - 1, y);
                if (x + 1 < width) enqueueIfBackground(x + 1, y);
                if (y > 0) enqueueIfBackground(x, y - 1);
                if (y + 1 < height) enqueueIfBackground(x, y + 1);
            }

            using (Graphics graphics = Graphics.FromImage(output))
            {
                graphics.Clear(Color.Transparent);
                graphics.DrawImageUnscaled(source, 0, 0);
            }

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (background[y * width + x])
                    {
                        output.SetPixel(x, y, Color.Transparent);
                    }
                }
            }

            string directory = System.IO.Path.GetDirectoryName(outputPath);
            if (!String.IsNullOrEmpty(directory))
            {
                System.IO.Directory.CreateDirectory(directory);
            }

            output.Save(outputPath, ImageFormat.Png);
        }
    }
}
'@

Add-Type -AssemblyName System.Drawing.Common
$runtimeDirectory = Split-Path -Parent ([System.Object].Assembly.Location)
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
[LightCheckerboardRemover]::Process($InputPath, $OutputPath, [String]::Join(';', $ExtraSeed))
