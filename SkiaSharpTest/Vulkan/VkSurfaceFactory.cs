using System;
using SkiaSharp;

namespace SkiaSharpTest.Vulkan
{
    public class VkSurfaceFactory : IDisposable
    {
        private readonly VkContext _vkContext;
        private readonly GRContext _grContext;

        public VkSurfaceFactory()
        {
            _vkContext = new VkContext();
            _grContext = CreateGrContext();
        }

        public SKSurface CreateSurface(SKImageInfo imageInfo)
        {
            var surface = SKSurface.Create(_grContext, true, imageInfo);
            return surface;
        }

        private GRContext CreateGrContext()
        {
            var ctx = _vkContext;
            var grVkBackendContext = new GRSharpVkBackendContext
            {
                VkInstance = ctx.Instance,
                VkPhysicalDevice = ctx.PhysicalDevice,
                VkDevice = ctx.Device,
                VkQueue = ctx.GraphicsQueue,
                GraphicsQueueIndex = ctx.GraphicsFamily,
                GetProcedureAddress = ctx.SharpVkGetProc,
            };

            var result = GRContext.CreateVulkan(grVkBackendContext);
            return result;
        }

        public void Dispose()
        {
            _grContext.Dispose();
            _vkContext.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
