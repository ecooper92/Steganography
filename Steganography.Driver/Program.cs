using System;
using System.IO;
using System.Text;
using Steganography.Core;

namespace Steganography.Driver
{
    class Program
    {
        static void Main(string[] args)
        {
            var inImage = @"./stock-photo.jpg";
            var outImage = @"./out.png";
            var processor = new ImageProcessor();

            var text = File.ReadAllText("./lorem-ipsum.txt");
            var bytes = Encoding.UTF8.GetBytes(text);
            processor.WriteData(bytes, inImage, outImage);

            var outBytes = processor.ReadData(outImage);
            var outText = Encoding.UTF8.GetString(outBytes);
            Console.WriteLine(outText);
        }
    }
}
