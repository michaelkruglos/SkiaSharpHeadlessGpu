using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

//using OpenTK.Graphics.ES30;
//using OpenTK.Mathematics;
//using OpenTK.Windowing.Desktop;
//using OpenTK.Windowing.GraphicsLibraryFramework;

// using SharpVk;
// using SharpVk.Khronos;

using SkiaSharp;

using SkiaSharpTest.Vulkan;

namespace SkiaSharpTest
{
    internal class Program
    {
        static void Main(string[] args)
        {
            //using var factory = new VkSurfaceFactory();
            Console.WriteLine("Start!");
            if (!Directory.Exists("output"))
            {
                Directory.CreateDirectory("output");
            }

            var fileNames = Enumerable.Range(0, 30).Select(i => $"image{i}.png").ToArray();
            var parallel = int.TryParse(Environment.GetEnvironmentVariable("TEST_PARALLEL"), out var parsed) ? parsed : 10;
            var sw = Stopwatch.StartNew();
            RunTest(fileNames, parallel, useVulkan: true);
            var vkElapsed = sw.Elapsed;
            sw.Restart();
            RunTest(fileNames, parallel, useVulkan: false);
            var cpuElapsed = sw.Elapsed;
            Console.WriteLine($"Vulkan: {vkElapsed}");
            Console.WriteLine($"CPU: {cpuElapsed}");
        }

        private static void RunTest(string[] fileNames, int parallel, bool useVulkan)
        {
            using var factory = new VkSurfaceFactory();
            Func<string, int, int, ISurface> method = useVulkan ? (name, w, h) => TestWithVulkanGPU(factory, name, w, h) : TestCpuOnly;
            var exceptions = RunConcurrentlyAsync(fileNames, parallel, fileName => method(fileName, 1920, 1080));
            foreach (var item in exceptions)
            {
                Console.WriteLine($"Error {item}");
            }
        }

        public static List<(string, Exception)> RunConcurrentlyAsync(IEnumerable<string> items, int concurrencyLimit, Func<string, ISurface> processItem)
        {
            var errors = new ConcurrentBag<(string, Exception)>();
            var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = concurrencyLimit };
            var results = new ConcurrentBag<(string, ISurface)>();

            Parallel.ForEach(items, parallelOptions, (item, _) =>
            {
                try
                {
                    var surface = processItem(item);
                    results.Add((item, surface));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error Processing item {item}:\n{ex}");
                    errors.Add((item, ex));
                }
            });

            foreach (var item in results)
            {
                SaveImage(item.Item2, item.Item1);
            }
            return errors.OrderBy(x => x.Item1).ToList();
        }

        public static object factoryLock = new object();

        private static ISurface TestWithVulkanGPU(VkSurfaceFactory factory, string fileName, int width, int height)
        {
            var number = int.Parse(string.Join("", fileName.Where(char.IsDigit)));
            ISurface surface;
            lock (factoryLock)
            {
                surface = factory.CreateSurface(new SKImageInfo(width, height));
            }

            DrawCircle(surface, number, width, height);
            return surface;
        }

        private static ISurface TestCpuOnly(string fileName, int width, int height)
        {
            var number = int.Parse(string.Join("", fileName.Where(char.IsDigit)));
            var surface = new SKSurfaceWrapper(SKSurface.Create(new SKImageInfo(width, height)));

            DrawCircle(surface, number, width, height);
            return surface;
        }

        private static void SaveImage(ISurface surface, string fileName)
        {
            var folder = Path.Join("./output", surface.Name);

            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            surface.SaveImage(Path.Join(folder, fileName), SKEncodedImageFormat.Png, 100);
        }

        private static void DrawCircle(ISurface surface, int number, int width, int height)
        {
            var canvas = surface.Canvas;
            var transparentBlue = new SKColor(0, 0, 255, 0);
            var colorBytes = new byte[3];
            Random.Shared.NextBytes(colorBytes);
            var random = new SKColor(colorBytes[0], colorBytes[1], colorBytes[2]);
            canvas.Clear(transparentBlue);
            var centerX = width / 2.0f;
            var centerY = height / 2.0f;
            var size = Math.Min(width, height) * 0.8f;
            using var circlePaint = new SKPaint { Color = random, Style = SKPaintStyle.StrokeAndFill, IsAntialias = true };
            canvas.DrawCircle(centerX - size/2, centerY - size/2, size, circlePaint);
            using var textPaint = new SKPaint
            {
                Color = new SKColor((byte)(255 - random.Red), (byte)(255 - random.Green), (byte)(255 - random.Blue), 128),
                Style = SKPaintStyle.StrokeAndFill,
                IsAntialias = true,
            };

            var font = new SKFont(SKTypeface.Default, size * 0.5f);
            font.Edging = SKFontEdging.SubpixelAntialias;
            font.Hinting = SKFontHinting.Full;
            canvas.DrawText($"ID: {number}", 400, 400, SKTextAlign.Center, font, textPaint);
        }

        private static void MakePicture()
        {

            using var bitmap = new SKBitmap(600, 600);
            using var canvas = new SKCanvas(bitmap);

            using var pictureRecorder = new SKPictureRecorder();
            using var recordingCanvas = pictureRecorder.BeginRecording(new SKRect(0, 0, bitmap.Width, bitmap.Height));

            var transparentBlue = new SKColor(0, 0, 255, 0);
            var violet = new SKColor(155,38,182);
            recordingCanvas.Clear(transparentBlue);
            recordingCanvas.DrawCircle(300, 300, 45, new SKPaint { Color = violet });

            using var picture = pictureRecorder.EndRecording();

            canvas.DrawPicture(picture);
            using var outfile = File.OpenWrite(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "output.png"));
            bitmap.Encode(SKEncodedImageFormat.Png, 100).SaveTo(outfile);

            using var pictureOutfile = File.OpenWrite(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "output.skp"));
            picture.Serialize(pictureOutfile);
        }
    }
}
