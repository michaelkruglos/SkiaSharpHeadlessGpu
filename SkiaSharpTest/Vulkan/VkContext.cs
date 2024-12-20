// using System;
// using System.Linq;
// using System.Runtime.InteropServices;
// using Silk.NET.Core;
// using Silk.NET.Core.Native;
// using Silk.NET.Vulkan;
// using Silk.NET.Vulkan.Extensions.KHR;
// using SkiaSharp;
//
// namespace SkiaSharpTest.Vulkan;
//
// public unsafe class VkContext : IDisposable
// {
//     bool EnableValidationLayers = true;
//
//     private readonly string[] validationLayers = new[]
//     {
//         "VK_LAYER_KHRONOS_validation"
//     };
//
//     private readonly string[] deviceExtensions = new[]
//     {
//         KhrSwapchain.ExtensionName
//     };
//     
//     public VkContext()
//     {
//         Instance = CreateInstance();
//
//         var filter = (PhysicalDevice device) =>
//         {
//             var deviceProperties = device.GetProperties();
//             return deviceProperties.DeviceType != PhysicalDeviceType.Cpu;
//         };
//
//         PhysicalDevice = Instance.EnumeratePhysicalDevices().FirstOrDefault(filter)
//                          ?? throw new Exception("Could not find non-CPU Vulkan device");
//
//         GraphicsFamily = FindGraphicsQueueFamily();
//
//         var queueInfos = new[]
//         {
//             new DeviceQueueCreateInfo { QueueFamilyIndex = GraphicsFamily, QueuePriorities = new[] { 1f } },
//         };
//
//         var candidateExtensions = new[]
//         {
//             KhrExtensions.GetMemoryRequirements2, KhrExtensions.BindMemory2,
//         };
//         var highVersion = (uint)PhysicalDevice.GetProperties().ApiVersion >= (uint)new Version(1, 1, 0);
//         string[] actualExtensions = /*highVersion ? candidateExtensions :*/ ChooseExtensions(candidateExtensions);
//         Device = PhysicalDevice.CreateDevice(queueInfos, layersToUse, actualExtensions);
//
//         GraphicsQueue = Device.GetQueue(GraphicsFamily, 0);
//
//         SharpVkGetProc = (name, instance, device) =>
//         {
//             const string funcname2 = "SharpVkGetProc";
//             var result = IntPtr.Zero;
//                 
//             // try device
//             if (device != null)
//             {
//                 result = device.GetProcedureAddress(name);
//             }
//
//             // otherwise try provided instance
//             if (result == IntPtr.Zero && instance != null)
//             {
//                 result = instance.GetProcedureAddress(name);
//             }
//
//             // fallback to the instance we created earlier
//             if (result == IntPtr.Zero)
//             {
//                 result = Instance.GetProcedureAddress(name);
//             }
//
//             if (result == IntPtr.Zero)
//             {
//                 Console.WriteLine($"[{funcname2}] Fetching KHR {name}");
//                 switch (name)
//                 {
//                     case "vkGetImageMemoryRequirements2":
//                     case "vkGetBufferMemoryRequirements2":
//                     case "vkGetImageSparseMemoryRequirements2":
//                     case "vkBindBufferMemory2":
//                     case "vkBindImageMemory2":
//                     case "vkTrimCommandPool":
//                     case "vkGetDescriptorSetLayoutSupport":
//                     case "vkCreateSamplerYcbcrConversion":
//                     case "vkDestroySamplerYcbcrConversion":
//                     {
//                         var actualName = name + "KHR";
//                         result = Device.GetProcedureAddress(actualName);
//                         if (result == IntPtr.Zero)
//                         {
//                             result = Instance.GetProcedureAddress(actualName);
//                         }
//
//                         break;
//                     }
//                     default:
//                         Console.WriteLine($"{funcname2}: Failed to get procedure address: {name}");
//                         break;
//                 }
//
//                 if (result == IntPtr.Zero)
//                 {
//                     Console.WriteLine($"{funcname2}: Failed to get procedure address: {name}KHR");
//                     return result;
//                 }
//             }
//
//             if (result == IntPtr.Zero)
//             {
//                 Console.WriteLine($"{funcname2}: Failed to get procedure address for {name}");
//             }
//
//             return result;
//         };
//             
//     }
//         
//     private string[] ChooseExtensions(string[] candidateExtensions)
//     {
//         var layers = Instance.EnumerateLayerProperties().Select(x => x.LayerName).ToList();
//         layers.Insert(0, null);
//         var deviceExtensions = layers.SelectMany(layer => PhysicalDevice.EnumerateDeviceExtensionProperties(layer))
//             .Select(x => x.ExtensionName).ToArray();
//         var extensions = deviceExtensions
//             .Intersect(candidateExtensions)
//             .ToArray();
//
//         if (candidateExtensions.Length != extensions.Length)
//         {
//             Console.WriteLine($"The following extensions are either missing or not enabled: {string.Join(", ", candidateExtensions.Except(extensions))}");
//         }
//
//         return extensions;
//     }
//
//     public Instance Instance { get; protected set; }
//
//     public PhysicalDevice PhysicalDevice { get; protected set; }
//
//     public Device Device { get; protected set; }
//
//     public Queue GraphicsQueue { get; protected set; }
//
//     //public virtual Queue PresentQueue { get; protected set; }
//
//     public uint GraphicsFamily { get; protected set; }
//
//     //public virtual uint PresentFamily { get; protected set; }
//
//     public GRVkGetProcedureAddressDelegate GetProc { get; protected set; }
//
//     public GRSharpVkGetProcedureAddressDelegate SharpVkGetProc { get; protected set; }
//
//     public virtual void Dispose()
//     {
//         // dispose all the disposable members
//         Device?.Dispose();
//         Surface?.Dispose();
//         Instance?.Dispose();
//         GC.SuppressFinalize(this);
//     }
//
//     protected uint FindGraphicsQueueFamily()
//     {
//         var queueFamilyProperties = PhysicalDevice.GetQueueFamilyProperties();
//
//         //queueFamilyProperties[0].
//         var graphicsFamilies = queueFamilyProperties
//             .Select((properties, index) => new { properties, index })
//             .Where(pair => pair.properties.QueueFlags.HasFlag(QueueFlags.Graphics))
//             .ToList();
//         if (graphicsFamilies.Count == 0)
//         {
//             throw new Exception("Unable to find graphics queue");
//         }
//
//         Console.WriteLine($"Found {graphicsFamilies.Count} graphics families");
//         foreach (var property in queueFamilyProperties)
//         {
//             Console.WriteLine(
//                 $"{property.QueueFlags:G}, Queues: {property.QueueCount}, Min image size: {property.MinImageTransferGranularity.Height}x{property.MinImageTransferGranularity.Width} by {property.MinImageTransferGranularity.Depth}");
//         }
//
//         return (uint)graphicsFamilies[0].index;
//     }
//     
//     private void CreateInstance()
//     {
//         var vk = Vk.GetApi();
//
//         if (EnableValidationLayers && !CheckValidationLayerSupport())
//         {
//             throw new Exception("validation layers requested, but not available!");
//         }
//         
//         ApplicationInfo appInfo = new()
//         {
//             SType = StructureType.ApplicationInfo,
//             PApplicationName = (byte*)Marshal.StringToHGlobalAnsi("Hello Triangle"),
//             ApplicationVersion = new Version32(1, 0, 0),
//             PEngineName = (byte*)Marshal.StringToHGlobalAnsi("No Engine"),
//             EngineVersion = new Version32(1, 0, 0),
//             ApiVersion = Vk.Version12
//         };
//
//         InstanceCreateInfo createInfo = new()
//         {
//             SType = StructureType.InstanceCreateInfo,
//             PApplicationInfo = &appInfo
//         };
//
//         var extensions = GetRequiredExtensions();
//         createInfo.EnabledExtensionCount = (uint)extensions.Length;
//         createInfo.PpEnabledExtensionNames = (byte**)SilkMarshal.StringArrayToPtr(extensions); ;
//
//         if (EnableValidationLayers)
//         {
//             createInfo.EnabledLayerCount = (uint)validationLayers.Length;
//             createInfo.PpEnabledLayerNames = (byte**)SilkMarshal.StringArrayToPtr(validationLayers);
//
//             DebugUtilsMessengerCreateInfoEXT debugCreateInfo = new();
//             PopulateDebugMessengerCreateInfo(ref debugCreateInfo);
//             createInfo.PNext = &debugCreateInfo;
//         }
//         else
//         {
//             createInfo.EnabledLayerCount = 0;
//             createInfo.PNext = null;
//         }
//
//         if (vk.CreateInstance(in createInfo, null, out instance) != Result.Success)
//         {
//             throw new Exception("failed to create instance!");
//         }
//
//         Marshal.FreeHGlobal((IntPtr)appInfo.PApplicationName);
//         Marshal.FreeHGlobal((IntPtr)appInfo.PEngineName);
//         SilkMarshal.Free((nint)createInfo.PpEnabledExtensionNames);
//
//         if (EnableValidationLayers)
//         {
//             SilkMarshal.Free((nint)createInfo.PpEnabledLayerNames);
//         }
//     }
//
// }