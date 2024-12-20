using System;
using Silk.NET.Vulkan;

namespace SkiaSharpTest.Vulkan;

public static class VulkanResultExtensions
{
    public static Result ThrowIfFailed(this Result result)
    {
        if (result != Result.Success)
        {
            throw new Exception($"Failed to create surface: {result}");
        }
        return result;
    }
}