using System;
using System.Dynamic;
using System.Linq;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using SkiaSharp;

namespace SkiaSharpTest.Vulkan;

public sealed class VkSurfaceFactory : IDisposable
{
    private Vk _vk;
    private Device _device;
    private PhysicalDevice _physicalDevice;
    private uint _graphicsQueueFamily;
    private Instance _instance;

    public VkSurfaceFactory()
    {
        _vk = Vk.GetApi();
        _instance = CreateInstance();
        _physicalDevice = GetPhysicalDevice();
        _graphicsQueueFamily = FindQueueFamily();
        _device = CreateDevice();
    }

    private unsafe uint FindQueueFamily()
    {
        uint propertiesCount = 0;
        _vk.GetPhysicalDeviceQueueFamilyProperties(_physicalDevice, &propertiesCount, null);
        Span<QueueFamilyProperties> queueFamilyProperties = stackalloc QueueFamilyProperties[(int)propertiesCount];
        _vk.GetPhysicalDeviceQueueFamilyProperties(_physicalDevice, &propertiesCount, queueFamilyProperties);
        var queueFamilyIndex = -1;
   
        // Find graphics queue family
        for (var i = 0; i < queueFamilyProperties.Length; i++)
        {
            if (queueFamilyProperties[i].QueueFlags.HasFlag(QueueFlags.GraphicsBit | QueueFlags.TransferBit))
            {
                queueFamilyIndex = i;
                break;
            }
        }

        if (queueFamilyIndex == -1)
        {
            throw new Exception("Failed to find queue family");
        }

        return (uint)queueFamilyIndex;
    }

    public ISurface CreateSurface(SKImageInfo imageInfo)
    {
        var renderTarget = new VulkanRenderTarget(_vk, _device, _physicalDevice, _graphicsQueueFamily,
            (uint)imageInfo.Width, (uint)imageInfo.Height);
        return renderTarget;
    }

    public unsafe void Dispose()
    {
        _vk.DestroyDevice(_device, null);
        _vk.DestroyInstance(_instance, null);
    }

    private static unsafe Instance CreateInstance()
    {
        var vk = Vk.GetApi();
    
        // Application info
        var appInfo = new ApplicationInfo
        {
            SType = StructureType.ApplicationInfo,
            PApplicationName = (byte*)SilkMarshal.StringToPtr("VkSurfaceFactory"),
            ApplicationVersion = Vk.MakeVersion(1, 0, 0),
            PEngineName = (byte*)SilkMarshal.StringToPtr("VkSurfaceFactory"),
            EngineVersion = Vk.MakeVersion(1, 0, 0),
            ApiVersion = Vk.Version12
        };

        // Instance create info
        var createInfo = new InstanceCreateInfo
        {
            SType = StructureType.InstanceCreateInfo,
            PApplicationInfo = &appInfo
        };

        // Create instance
        try
        {
            Instance instance;
            vk.CreateInstance(in createInfo, null, &instance).ThrowIfFailed();
            return instance;
        }
        finally
        {
            // Cleanup
            SilkMarshal.Free((nint)appInfo.PApplicationName);
            SilkMarshal.Free((nint)appInfo.PEngineName);
        }
    }

    private unsafe Device CreateDevice()
    {
        float priority = 1.0f;
        var queueCreateInfo = new DeviceQueueCreateInfo
        {
            SType = StructureType.DeviceQueueCreateInfo,
            QueueFamilyIndex = _graphicsQueueFamily,
            QueueCount = 1,
            PQueuePriorities = &priority
        };

        var deviceFeatures = new PhysicalDeviceFeatures();
        var createInfo = new DeviceCreateInfo
        {
            SType = StructureType.DeviceCreateInfo,
            QueueCreateInfoCount = 1,
            PQueueCreateInfos = &queueCreateInfo,
            PEnabledFeatures = &deviceFeatures
        };

        Device device;
        _vk.CreateDevice(_physicalDevice, &createInfo, null, &device).ThrowIfFailed();
        return device;
    }

    private PhysicalDevice GetPhysicalDevice()
    {
        return _vk.GetPhysicalDevices(_instance)
            .First(d => _vk.GetPhysicalDeviceProperties(d).DeviceType != PhysicalDeviceType.Cpu);
    }
}