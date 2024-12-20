using System;
using System.IO;
using SkiaSharp;

namespace SkiaSharpTest;

public interface ISurface : IDisposable
{
    string Name { get; }
    SKCanvas Canvas { get; }
    void Flush();
    
    public void SaveImage(string filePath, SKEncodedImageFormat format, int quality);
}

public sealed class SKSurfaceWrapper : ISurface
{
    public string Name => "regular";
    private SKSurface _surfaceImplementation;

    public SKSurfaceWrapper(SKSurface surfaceImplementation)
    {
        _surfaceImplementation = surfaceImplementation;
    }

    public void Dispose()
    {
        _surfaceImplementation.Dispose();
    }

    public SKCanvas Canvas => _surfaceImplementation.Canvas;

    public void Flush()
    {
        _surfaceImplementation.Flush();
    }

    public void SaveImage(string filePath, SKEncodedImageFormat format, int quality)
    {
        using var data = _surfaceImplementation.Snapshot().Encode(format, quality);
        using var fileStream = File.OpenWrite(filePath);
        data.SaveTo(fileStream);
    }
}