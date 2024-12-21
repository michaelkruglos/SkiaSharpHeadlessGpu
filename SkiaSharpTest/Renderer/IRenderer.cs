using System;
using System.Collections.Generic;
using System.Threading.Channels;
using System.Threading.Tasks;
using SkiaSharp;

namespace SkiaSharpTest.Renderer;


public record RenderResult(int FrameIndex, ISurface Surface);

public interface IRenderer
{
    IAsyncEnumerable<RenderResult> Render(SKImageInfo frameImageInfo, IReadOnlyList<Func<ISurfaceFactory, SKImageInfo, ValueTask<ISurface>>> frameMethods);
}

public class SequentialRenderer : IRenderer
{
    private ISurfaceFactory _factory;
    public SequentialRenderer(ISurfaceFactory factory)
    {
        _factory = factory;
    }

    public async IAsyncEnumerable<RenderResult> Render(SKImageInfo frameImageInfo, IReadOnlyList<Func<ISurfaceFactory, SKImageInfo, ValueTask<ISurface>>> frameMethods)
    {
        for (var i = 0; i < frameMethods.Count; i++)
        {
            var frameMethod = frameMethods[i];
            yield return new RenderResult(i, await frameMethod.Invoke(_factory, frameImageInfo));
        }
    }
}

public class ParallelRenderer : IRenderer
{
    private int _parallelCount;
    private ISurfaceFactory _factory;

    public ParallelRenderer(ISurfaceFactory factory, int parallelCount)
    {
        _factory = factory;
        _parallelCount = parallelCount;
    }

    public async IAsyncEnumerable<RenderResult> Render(SKImageInfo frameImageInfo, IReadOnlyList<Func<ISurfaceFactory, SKImageInfo, ValueTask<ISurface>>> frameMethods)
    {
        var channel = Channel.CreateBounded<RenderResult>(_parallelCount);
        var parallelTask = Parallel.ForAsync(0, frameMethods.Count, async (i, ct) =>
        {
            var frameMethod = frameMethods[i];
            var result = await frameMethod.Invoke(_factory, frameImageInfo);
            await channel.Writer.WriteAsync(new RenderResult(i, result), ct);
        });

        for (var i = 0; i < frameMethods.Count; i++)
        {
            yield return await channel.Reader.ReadAsync();
        }

        channel.Writer.Complete(); // cleanup
        await parallelTask; // to propagate exceptions
    }
}