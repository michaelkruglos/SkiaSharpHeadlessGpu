using System;
using System.IO;
using Silk.NET.Core;
using Silk.NET.Vulkan;
using SkiaSharp;
using Buffer = System.Buffer;

namespace SkiaSharpTest.Vulkan;

public sealed unsafe class VulkanRenderTarget : ISurface
{
    private readonly Vk _vk;
    private readonly Device _device;
    private readonly PhysicalDevice _physicalDevice;
    private Image _image;
    private DeviceMemory _imageMemory;
    private readonly uint _width;
    private readonly uint _height;
    private readonly ulong _imageSize;
    
    private readonly SKSurface _surface;
    private readonly GRContext _grContext;
    private readonly GRBackendRenderTarget _renderTarget;
    
    private CommandPool _commandPool;

    public Image Image => _image;
    
    private readonly uint _graphicsQueueFamily;

    public string Name => "vulkan";
    
    public VulkanRenderTarget(Vk vk, Device device, PhysicalDevice physicalDevice, uint graphicsQueueFamily, uint width, uint height)
    {
        _vk = vk;
        _device = device;
        _physicalDevice = physicalDevice;
        _width = width;
        _height = height;
        _graphicsQueueFamily = graphicsQueueFamily;

        CreateImage();
        CreateCommandPool();
        var requirementsInfo = new ImageMemoryRequirementsInfo2 
        {
            SType = StructureType.ImageMemoryRequirementsInfo2,
            Image = _image
        };
        var requirements = new MemoryRequirements2 { SType = StructureType.MemoryRequirements2 };
        _vk.GetImageMemoryRequirements2(_device, &requirementsInfo, &requirements);
        _imageSize = requirements.MemoryRequirements.Size;
        var appInfo = new ApplicationInfo
        {
            SType = StructureType.ApplicationInfo,
            ApiVersion = Vk.Version12
        };

        var createInfo = new InstanceCreateInfo
        {
            SType = StructureType.InstanceCreateInfo,
            PApplicationInfo = &appInfo
        };

        Instance instance;
        if (vk.CreateInstance(in createInfo, null, &instance) != Result.Success)
        {
            throw new Exception("Failed to create instance");
        }

        Queue graphicsQueue;
        _vk.GetDeviceQueue(device, graphicsQueueFamily, 0, &graphicsQueue);
        var backendContext = new GRVkBackendContext
        {
            VkInstance = instance.Handle,
            VkPhysicalDevice = physicalDevice.Handle,
            VkDevice = device.Handle,
            VkQueue = graphicsQueue.Handle,
            GraphicsQueueIndex = graphicsQueueFamily,
            GetProcedureAddress = GetVulkanProcAddress,
            MaxAPIVersion = new Version32(1, 2, 0),
        };

        _grContext = GRContext.CreateVulkan(backendContext);
        
        var imageInfo = new GRVkImageInfo
        {
            Image = _image.Handle,
            ImageLayout = (int)ImageLayout.ColorAttachmentOptimal,
            Format = (int)Format.R8G8B8A8Unorm,
            ImageTiling = (int)ImageTiling.Linear,
            Alloc = new GRVkAlloc { Memory = _imageMemory.Handle, Flags = 0, Offset = 0, Size = _imageSize }
        };

        _renderTarget = new GRBackendRenderTarget(
            (int)_width, (int)_height, 
            1, // sampleCount
            imageInfo);

        _surface = SKSurface.Create(_grContext, _renderTarget, GRSurfaceOrigin.TopLeft, SKColorType.Rgba8888);
    }
    
    public SKCanvas Canvas => _surface.Canvas;

    private IntPtr GetVulkanProcAddress(string name, IntPtr instance, IntPtr device)
    {
        if (device != IntPtr.Zero)
        {
            var procAddr = _vk.GetDeviceProcAddr(new Device(device), name);
            if (procAddr != IntPtr.Zero) return procAddr;
        }

        return _vk.GetInstanceProcAddr(new Instance(instance), name);
    }
    
    public byte[] GetImageData()
    {
        void* mappedMemory = MapMemory();
        byte[] imageData = new byte[_imageSize];

        fixed (void* ptr = imageData)
        {
            Buffer.MemoryCopy(mappedMemory, ptr, _imageSize, _imageSize);
        }
        
        UnmapMemory();
        return imageData;
    }

    private void CreateImage()
    {
        // Image creation info
        var imageInfo = new ImageCreateInfo
        {
            SType = StructureType.ImageCreateInfo,
            ImageType = ImageType.Type2D,
            Format = Format.R8G8B8A8Unorm,  // Compatible with SkiaSharp
            Extent = new Extent3D(_width, _height, 1),
            MipLevels = 1,
            ArrayLayers = 1,
            Samples = SampleCountFlags.Count1Bit,
            Tiling = ImageTiling.Linear,  // Linear for easy CPU access
            Usage = ImageUsageFlags.TransferSrcBit | ImageUsageFlags.ColorAttachmentBit,
            SharingMode = SharingMode.Exclusive,
            InitialLayout = ImageLayout.Undefined
        };

        // Create the image
        fixed (Image* imagePtr = &_image)
        {
            if (_vk.CreateImage(_device, in imageInfo, null, imagePtr) != Result.Success)
            {
                throw new Exception("Failed to create image!");
            }
        }

        // Get memory requirements
        var memRequirements = new MemoryRequirements();
        _vk.GetImageMemoryRequirements(_device, _image, &memRequirements);

        // Find suitable memory type
        var memProperties = new PhysicalDeviceMemoryProperties();
        _vk.GetPhysicalDeviceMemoryProperties(_physicalDevice, &memProperties);

        int memoryTypeIndex = FindMemoryType(
            memRequirements.MemoryTypeBits,
            MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit,
            memProperties);

        // Allocate memory
        var allocInfo = new MemoryAllocateInfo
        {
            SType = StructureType.MemoryAllocateInfo,
            AllocationSize = memRequirements.Size,
            MemoryTypeIndex = (uint)Math.Clamp(memoryTypeIndex, uint.MinValue, uint.MaxValue),
        };

        fixed (DeviceMemory* memoryPtr = &_imageMemory)
        {
            if (_vk.AllocateMemory(_device, in allocInfo, null, memoryPtr) != Result.Success)
            {
                throw new Exception("Failed to allocate image memory!");
            }
        }

        // Bind image memory
        _vk.BindImageMemory(_device, _image, _imageMemory, 0);
    }
    
    private void CreateCommandPool()
    {
        var poolInfo = new CommandPoolCreateInfo
        {
            SType = StructureType.CommandPoolCreateInfo,
            QueueFamilyIndex = _graphicsQueueFamily,
            Flags = CommandPoolCreateFlags.ResetCommandBufferBit
        };

        fixed (CommandPool* poolPtr = &_commandPool)
        {
            if (_vk.CreateCommandPool(_device, in poolInfo, null, poolPtr) != Result.Success)
                throw new Exception("Failed to create command pool");
        }
    }

    private void* MapMemory()
    {
        void* data;
        _vk.MapMemory(_device, _imageMemory, 0, Vk.WholeSize, 0, &data);
        return data;
    }

    private void UnmapMemory()
    {
        _vk.UnmapMemory(_device, _imageMemory);
    }

    private int FindMemoryType(uint typeFilter, MemoryPropertyFlags properties, PhysicalDeviceMemoryProperties memProperties)
    {
        for (int i = 0; i < memProperties.MemoryTypeCount; i++)
        {
            if ((typeFilter & (1u << i)) != 0 &&
                (memProperties.MemoryTypes[i].PropertyFlags & properties) == properties)
            {
                return i;
            }
        }

        throw new Exception("Failed to find suitable memory type!");
    }

    public void Flush()
    {
        _surface.Flush(submit: true, synchronous: true);
        _grContext.Flush(submit: true, synchronous: true);
    }

    public byte[] GetPng(SKEncodedImageFormat format, int quality)
    {
        Flush();
        var info = new SKImageInfo((int)_width, (int)_height, SKColorType.Rgba8888, SKAlphaType.Premul);
        var imageData = GetImageData();

        fixed (void* ptr = imageData)
        {
            using var skData = SKData.Create((IntPtr)ptr, (int)_imageSize);
            using var skImage = SKImage.FromPixels(info, skData);
            using var data = skImage.Encode(format, quality);
            return data.ToArray();
        }
    }

    public void Dispose()
    {
        _vk.DestroyCommandPool(_device, _commandPool, null);
        _vk.DestroyImage(_device, _image, null);
        _vk.FreeMemory(_device, _imageMemory, null);
        _surface.Dispose();
        _renderTarget.Dispose();
        _grContext.Dispose();
    }
}
