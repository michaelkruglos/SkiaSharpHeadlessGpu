using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using SkiaSharp;
using SkiaSharpTest.Renderer;
using SkiaSharpTest.Vulkan;

namespace SkiaSharpTest;

internal class Program
{
    static async Task Main(string[] args)
    {
        //using var factory = new VkSurfaceFactory();
        Console.WriteLine("Start!");
        const string outputDir = "output";
        var regularDir = Path.Combine(outputDir, "regular");
        var vulkanDir = Path.Combine(outputDir, "vulkan");
        var directories = new[] { outputDir, regularDir, vulkanDir };
        foreach (var directory in directories)
        {
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory!);
            }
        }

        var parallelism = int.TryParse(Environment.GetEnvironmentVariable("TEST_PARALLEL"), out var parsed)
            ? parsed
            : Environment.ProcessorCount;

        var skiaFactory = new SkiaSurfaceFactory();
        var vulkanFactory = new VkSurfaceFactory();
        var sequentialCpuRenderer = new SequentialRenderer(skiaFactory);
        var sequentialVkRenderer = new SequentialRenderer(vulkanFactory);
        var parallelCpuRenderer = new ParallelRenderer(skiaFactory, parallelism);
        var parallelVkRenderer = new ParallelRenderer(vulkanFactory, parallelism);

        var frameMethods = Enumerable.Range(0, 30)
            .Select<int, Func<ISurfaceFactory, SKImageInfo, ValueTask<ISurface>>>(i =>
                async ValueTask<ISurface> (ISurfaceFactory factory, SKImageInfo imageInfo) =>
                {
                    try
                    {
                        var surface = factory.CreateSurface(imageInfo);
                        DrawCircle(surface, i, imageInfo.Width, imageInfo.Height);
                        return surface;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                        throw;
                    }
                })
            .ToArray();

        var sw = Stopwatch.StartNew();
        await foreach (var result in sequentialCpuRenderer.Render(new SKImageInfo(1920, 1080), frameMethods))
        {
            var png = result.Surface.GetPng(SKEncodedImageFormat.Png, 100);
            await File.WriteAllBytesAsync(Path.Join(regularDir, $"image{result.FrameIndex}.png"), png);
            result.Surface.Dispose();
        }

        Console.WriteLine($"Sequential CPU renderer Elapsed: {sw.Elapsed}");
        
        sw.Restart();
        await foreach (var result in sequentialVkRenderer.Render(new SKImageInfo(1920, 1080), frameMethods))
        {
            var png = result.Surface.GetPng(SKEncodedImageFormat.Png, 100);
            await File.WriteAllBytesAsync(Path.Join(vulkanDir, $"image{result.FrameIndex}.png"), png);
            result.Surface.Dispose();
        }
        Console.WriteLine($"Sequential Vulkan renderer Elapsed: {sw.Elapsed}");

        sw.Restart();
        await foreach (var result in parallelCpuRenderer.Render(new SKImageInfo(1920, 1080), frameMethods))
        {
            var png = result.Surface.GetPng(SKEncodedImageFormat.Png, 100);
            await File.WriteAllBytesAsync(Path.Join(regularDir, $"image{result.FrameIndex}.png"), png);
            result.Surface.Dispose();
        }
        Console.WriteLine($"Parallel CPU renderer Elapsed: {sw.Elapsed}");

        sw.Restart();
        await foreach (var result in parallelVkRenderer.Render(new SKImageInfo(1920, 1080), frameMethods))
        {
            var png = result.Surface.GetPng(SKEncodedImageFormat.Png, 100);
            await File.WriteAllBytesAsync(Path.Join(vulkanDir, $"image{result.FrameIndex}.png"), png);
            result.Surface.Dispose();
        }
        Console.WriteLine($"Parallel Vulkan renderer Elapsed: {sw.Elapsed}");
    }


    private static void SaveImage(ISurface surface, string fileName)
    {
        var folder = Path.Join("./output", surface.Name);

        if (!Directory.Exists(folder))
        {
            Directory.CreateDirectory(folder);
        }

        var png = surface.GetPng(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(Path.Join(folder, fileName), png);
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
        random.ToHsl(out var randomHue, out var randomSaturation, out var randomLightness);
        using var textPaint = new SKPaint
        {
            Color = SKColor.FromHsl(randomHue, randomSaturation, randomLightness > 0.5 ? 0 : 100),
            Style = SKPaintStyle.StrokeAndFill,
            IsAntialias = true,
        };

        var font = new SKFont(SKTypeface.Default, size * 0.5f);
        font.Edging = SKFontEdging.SubpixelAntialias;
        font.Hinting = SKFontHinting.Full;
        canvas.DrawText($"ID: {number}", 400, 400, SKTextAlign.Center, font, textPaint);
    }
}