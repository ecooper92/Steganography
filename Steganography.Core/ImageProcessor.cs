using System;
using System.IO;
using System.Linq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Collections.Generic;

namespace Steganography.Core
{
    public class ImageProcessor
    {
        /// <summary>
        /// Attempt to write data to image.
        /// </summary>
        public void WriteData(byte[] data, string srcImagePath, string destImagePath)
        {
            var seed = (int)DateTime.Now.Ticks;
            using (var image = (Image<Rgba32>)Image<Rgba32>.Load(srcImagePath))
            {
                var pixels = SelectPixels(seed, image.Bounds(), data.Length);
                WriteInt32AverageBlock(image, data.Length, 1, image.Height - 2);
                WriteInt32AverageBlock(image, seed, image.Width - 2, image.Height - 2);

                for (int i = 0; i < pixels.Length; i++)
                {
                    WriteAveragePixel(image, pixels[i].X, pixels[i].Y, data[i]);
                }

                using (var file = File.Create(destImagePath))
                {
                    image.SaveAsPng(file);
                }
            }
        }

        /// <summary>
        /// Attempt to read data from image.
        /// </summary>
        public byte[] ReadData(string srcImagePath)
        {
            using (var image = (Image<Rgba32>)Image<Rgba32>.Load(srcImagePath))
            {
                var seed = ReadInt32AverageBlock(image, image.Width - 2, image.Height - 2);
                var data = new byte[ReadInt32AverageBlock(image, 1, image.Height - 2)];
                var pixels = SelectPixels(seed, image.Bounds(), data.Length);

                for (int i = 0; i < pixels.Length; i++)
                {
                    data[i] = ReadAveragePixel(image, pixels[i].X, pixels[i].Y);
                }

                return data;
            }
        }

        /// <summary>
        /// Scales a value to a new range.
        /// </summary>
        private double Scale(double current, double currentMin, double currentMax, double newMin, double newMax)
        {
            var currentWidth = currentMax - currentMin;
            var newWidth = newMax - newMin;
            return (((current - currentMin) / currentWidth) * newWidth) + newMin;
        }

        /// <summary>
        /// Average the provided colors and scale the range.
        /// </summary>
        private Rgba32 GetColorAverage(IEnumerable<Rgba32> colors, int size)
        {
            int rSum = 0;
            int gSum = 0;
            int bSum = 0;
            int length = 0;

            var half = size / 2;
            foreach (var color in colors)
            {
                rSum += (byte)Scale(color.R, 0, 255, half, 255 - half);
                gSum += (byte)Scale(color.G, 0, 255, half, 255 - half);
                bSum += (byte)Scale(color.B, 0, 255, half, 255 - half);
                length++;
            }

            return new Rgba32((byte)(rSum / length), (byte)(gSum / length), (byte)(bSum / length));
        }

        /// <summary>
        /// Randomly select a number of (x,y) positions within a bounded area.
        /// </summary>
        private (int X, int Y)[] SelectPixels(int seed, Rectangle bounds, int count, int boundOffset = 2, double densityFactor = 0.35)
        {
            var availableSize = (int)((bounds.Width - (boundOffset * 2)) * (bounds.Height - (boundOffset * 2)) * densityFactor);
            if (count > availableSize)
            {
                throw new ArgumentException("No enough pixel space.");
            }

            var random = new Random(seed);
            var pixels = new HashSet<(int X, int Y)>();
            while (pixels.Count < count)
            {
                var x = random.Next(bounds.X + boundOffset, bounds.X + bounds.Width - boundOffset);
                var y = random.Next(bounds.Y + boundOffset, bounds.Y + bounds.Height - boundOffset);

                if (!pixels.Contains((x, y))
                    && !pixels.Contains((x - 1, y))
                    && !pixels.Contains((x + 1, y))
                    && !pixels.Contains((x, y - 1))
                    && !pixels.Contains((x, y + 1)))
                {
                    pixels.Add((x, y));
                }
            }

            return pixels.ToArray();
        }

        /// <summary>
        /// Write a 32 bit integer as a pixel.
        /// </summary>
        private void WriteInt32(Image<Rgba32> image, int value, int x, int y)
        {
            var r = (byte)((value >> 24) & 255);
            var g = (byte)((value >> 16) & 255);
            var b = (byte)((value >> 8) & 255);
            var a = (byte)((value >> 0) & 255);
            image.GetPixelRowSpan(y)[x] = new Rgba32(r, g, b, a);
        }

        /// <summary>
        /// Write a 32 bit integer as a block of pixels.
        /// </summary>
        private void WriteInt32AverageBlock(Image<Rgba32> image, int value, int x, int y)
        {
            WriteAveragePixel(image, x - 1, y - 1, (byte)((value >> 0) & 255));
            WriteAveragePixel(image, x - 1, y + 1, (byte)((value >> 8) & 255));
            WriteAveragePixel(image, x + 1, y - 1, (byte)((value >> 16) & 255));
            WriteAveragePixel(image, x + 1, y + 1, (byte)((value >> 24) & 255));
        }

        /// <summary>
        /// Read a 32 bit integer from a pixel.
        /// </summary>
        private int ReadInt32(Image<Rgba32> image, int x, int y)
        {
            var data = image.GetPixelRowSpan(y)[x];
            return (data.R << 24) | (data.G << 16) | (data.B << 8) | data.A;
        }

        /// <summary>
        /// Read a 32 bit integer from a pixel.
        /// </summary>
        private int ReadInt32AverageBlock(Image<Rgba32> image, int x, int y)
        {
            var p1 = ReadAveragePixel(image, x - 1, y - 1);
            var p2 = ReadAveragePixel(image, x - 1, y + 1);
            var p3 = ReadAveragePixel(image, x + 1, y - 1);
            var p4 = ReadAveragePixel(image, x + 1, y + 1);
            return (p4 << 24) | (p3 << 16) | (p2 << 8) | p1;
        }

        /// <summary>
        /// Get the pixel data of the surrounding points.
        /// </summary>
        private IEnumerable<Rgba32> GetSurroundingPixels(Image<Rgba32> image, int x, int y)
        {
            var pixels = new List<Rgba32>();

            var rowData = image.GetPixelRowSpan(y);
            if (x > 0)
            {
                pixels.Add(rowData[x - 1]);
            }
            if (x < image.Width - 1)
            {
                pixels.Add(rowData[x + 1]);
            }
            if (y > 0)
            {
                pixels.Add(image.GetPixelRowSpan(y - 1)[x]);
            }
            if (y < image.Height - 1)
            {
                pixels.Add(image.GetPixelRowSpan(y + 1)[x]);
            }

            return pixels;
        }

        /// <summary>
        /// Writes a byte to a pixel using the average color of the surrounding pixels.
        /// </summary>
        private void WriteAveragePixel(Image<Rgba32> image, int x, int y, byte value)
        {
            var pixels = GetSurroundingPixels(image, x, y);
            var average = GetColorAverage(pixels, 8);

            var rOffset = (value >> 6) & 7;
            var gOffset = (value >> 3) & 7;
            var bOffset = (value >> 0) & 7;

            var rowData = image.GetPixelRowSpan(y);
            rowData[x].R = (byte)(average.R + rOffset - 2);
            rowData[x].G = (byte)(average.G + gOffset - 4);
            rowData[x].B = (byte)(average.B + bOffset - 4);
        }

        /// <summary>
        /// Reads a byte from a pixel using the average color of the surrounding pixels.
        /// </summary>
        private byte ReadAveragePixel(Image<Rgba32> image, int x, int y)
        {            
            var pixels = GetSurroundingPixels(image, x, y);
            var average = GetColorAverage(pixels, 8);

            var rowData = image.GetPixelRowSpan(y);
            var rOffset = rowData[x].R - average.R + 2;
            var gOffset = rowData[x].G - average.G + 4;
            var bOffset = rowData[x].B - average.B + 4;
            return (byte)((rOffset << 6) | (gOffset << 3) | bOffset);
        }
    }
}
