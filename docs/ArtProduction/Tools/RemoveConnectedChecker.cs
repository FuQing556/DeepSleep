using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;

namespace DeepSleep.ArtProduction
{
    /// <summary>
    /// Removes a baked light-neutral checkerboard only when it is connected to
    /// the image border. Dark outlined artwork and enclosed highlights remain.
    /// This is a deterministic repair for generated PNGs that simulated alpha.
    /// </summary>
    public static class RemoveConnectedChecker
    {
        public static void Run(string inputPath, string outputPath)
        {
            using (var source = new Bitmap(inputPath))
            using (var output = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb))
            {
                using (var graphics = Graphics.FromImage(output))
                {
                    graphics.DrawImageUnscaled(source, 0, 0);
                }

                var width = output.Width;
                var height = output.Height;
                var visited = new bool[width * height];
                var queue = new Queue<int>();

                Action<int, int> enqueueIfBackground = (x, y) =>
                {
                    var index = y * width + x;
                    if (visited[index]) return;
                    var color = output.GetPixel(x, y);
                    if (!IsNeutralChecker(color)) return;
                    visited[index] = true;
                    queue.Enqueue(index);
                };

                for (var x = 0; x < width; x++)
                {
                    enqueueIfBackground(x, 0);
                    enqueueIfBackground(x, height - 1);
                }

                for (var y = 0; y < height; y++)
                {
                    enqueueIfBackground(0, y);
                    enqueueIfBackground(width - 1, y);
                }

                while (queue.Count > 0)
                {
                    var index = queue.Dequeue();
                    var x = index % width;
                    var y = index / width;

                    output.SetPixel(x, y, Color.FromArgb(0, 0, 0, 0));

                    if (x > 0) enqueueIfBackground(x - 1, y);
                    if (x + 1 < width) enqueueIfBackground(x + 1, y);
                    if (y > 0) enqueueIfBackground(x, y - 1);
                    if (y + 1 < height) enqueueIfBackground(x, y + 1);
                }

                output.Save(outputPath, ImageFormat.Png);
            }
        }

        private static bool IsNeutralChecker(Color color)
        {
            var min = Math.Min(color.R, Math.Min(color.G, color.B));
            var max = Math.Max(color.R, Math.Max(color.G, color.B));
            return min >= 205 && max - min <= 18;
        }
    }
}
