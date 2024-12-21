using System;
using System.IO;
using SkiaSharp;

namespace SkiaSharpTest;

public interface ISurface : IDisposable
{
    string Name { get; }
    SKCanvas Canvas { get; }
    void Flush();
    
    public byte[] GetPng(SKEncodedImageFormat format, int quality);
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

    public byte[] GetPng(SKEncodedImageFormat format, int quality)
    {
        _surfaceImplementation.Flush(submit: true, synchronous: true);
        using var data = _surfaceImplementation.Snapshot();
        using var encoded = data.Encode(format, quality);
        return encoded.ToArray();
    }
}