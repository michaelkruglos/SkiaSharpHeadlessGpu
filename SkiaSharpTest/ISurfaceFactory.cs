using System;
using SkiaSharp;

namespace SkiaSharpTest;

public interface ISurfaceFactory : IDisposable
{
    ISurface CreateSurface(SKImageInfo imageInfo);
}

public sealed class SkiaSurfaceFactory : ISurfaceFactory
{
    public ISurface CreateSurface(SKImageInfo imageInfo)
    {
        return new SKSurfaceWrapper(SKSurface.Create(imageInfo));
    }

    public void Dispose()
    {
    }
}