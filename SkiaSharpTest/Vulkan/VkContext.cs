using System;
using System.Linq;

using SharpVk;
using SharpVk.Khronos;
using SharpVk.Multivendor;
using SkiaSharp;

using Device = SharpVk.Device;
using Instance = SharpVk.Instance;
using PhysicalDevice = SharpVk.PhysicalDevice;
using Queue = SharpVk.Queue;
using Version = SharpVk.Version;

namespace SkiaSharpTest.Vulkan
{
    public class VkContext : IDisposable
    {
        public VkContext()
        {
            var extensions = new[]
            {
                KhrExtensions.Surface, KhrExtensions.GetPhysicalDeviceProperties2,
                //   "VK_EXT_debug_utils",
                //"VK_EXT_debug_report"

            };

            var appInfo = new ApplicationInfo
            {
                ApplicationName = "vulkan",
                ApplicationVersion = new Version(1,0,0),
                EngineName = "SkiaSharpGpuHeadless",
                EngineVersion = new Version(1,0,0),
                ApiVersion = new Version(1,2,0),
            };

            string[] layersToUse = null; // new[] { "VK_LAYER_KHRONOS_validation" };

            var debugReport = new DebugReportCallbackCreateInfo
            {
                Callback = (DebugReportFlags flags, DebugReportObjectType objectType, ulong @object,
                    HostSize location, int messageCode, string pLayerPrefix, string pMessage,
                    IntPtr pUserData) =>
                {
                    Console.WriteLine($"[{flags:G}] {pLayerPrefix} Code {messageCode}: {pMessage}");
                    Console.Out.Flush();
                    return true;
                },
                Flags = DebugReportFlags.Error | DebugReportFlags.Warning |
                        DebugReportFlags.PerformanceWarning,
            };
            Instance = Instance.Create(layersToUse, extensions, null, appInfo)
                       ?? throw new InvalidOperationException("Failed to create instance.");

            var filter = (PhysicalDevice device) =>
            {
                var deviceProperties = device.GetProperties();
                return deviceProperties.DeviceType != PhysicalDeviceType.Cpu;
            };

            PhysicalDevice = Instance.EnumeratePhysicalDevices().FirstOrDefault(filter)
                             ?? throw new Exception("Could not find non-CPU Vulkan device");

            GraphicsFamily = FindGraphicsQueueFamily();

            var queueInfos = new[]
            {
                new DeviceQueueCreateInfo { QueueFamilyIndex = GraphicsFamily, QueuePriorities = new[] { 1f } },
            };

            var candidateExtensions = new[]
            {
                KhrExtensions.GetMemoryRequirements2, KhrExtensions.BindMemory2,
            };
            var highVersion = (uint)PhysicalDevice.GetProperties().ApiVersion >= (uint)new Version(1, 1, 0);
            string[] actualExtensions = /*highVersion ? candidateExtensions :*/ ChooseExtensions(candidateExtensions);
            Device = PhysicalDevice.CreateDevice(queueInfos, layersToUse, actualExtensions);

            GraphicsQueue = Device.GetQueue(GraphicsFamily, 0);

            SharpVkGetProc = (name, instance, device) =>
            {
                const string funcname2 = "SharpVkGetProc";
                var result = IntPtr.Zero;
                
                // try device
                if (device != null)
                {
                    result = device.GetProcedureAddress(name);
                }

                // otherwise try provided instance
                if (result == IntPtr.Zero && instance != null)
                {
                    result = instance.GetProcedureAddress(name);
                }

                // fallback to the instance we created earlier
                if (result == IntPtr.Zero)
                {
                    result = Instance.GetProcedureAddress(name);
                }

                if (result == IntPtr.Zero)
                {
                    Console.WriteLine($"[{funcname2}] Fetching KHR {name}");
                    switch (name)
                    {
                        case "vkGetImageMemoryRequirements2":
                        case "vkGetBufferMemoryRequirements2":
                        case "vkGetImageSparseMemoryRequirements2":
                        case "vkBindBufferMemory2":
                        case "vkBindImageMemory2":
                        case "vkTrimCommandPool":
                        case "vkGetDescriptorSetLayoutSupport":
                        case "vkCreateSamplerYcbcrConversion":
                        case "vkDestroySamplerYcbcrConversion":
                        {
                            var actualName = name + "KHR";
                            result = Device.GetProcedureAddress(actualName);
                            if (result == IntPtr.Zero)
                            {
                                result = Instance.GetProcedureAddress(actualName);
                            }

                            break;
                        }
                        default:
                            Console.WriteLine($"{funcname2}: Failed to get procedure address: {name}");
                            break;
                    }

                    if (result == IntPtr.Zero)
                    {
                        Console.WriteLine($"{funcname2}: Failed to get procedure address: {name}KHR");
                        return result;
                    }
                }

                if (result == IntPtr.Zero)
                {
                    Console.WriteLine($"{funcname2}: Failed to get procedure address for {name}");
                }

                return result;
            };
            
        }
        
        private string[] ChooseExtensions(string[] candidateExtensions)
        {
            var layers = Instance.EnumerateLayerProperties().Select(x => x.LayerName).ToList();
            layers.Insert(0, null);
            var deviceExtensions = layers.SelectMany(layer => PhysicalDevice.EnumerateDeviceExtensionProperties(layer))
                .Select(x => x.ExtensionName).ToArray();
            var extensions = deviceExtensions
                .Intersect(candidateExtensions)
                .ToArray();

            if (candidateExtensions.Length != extensions.Length)
            {
                Console.WriteLine($"The following extensions are either missing or not enabled: {string.Join(", ", candidateExtensions.Except(extensions))}");
            }

            return extensions;
        }

        public Instance Instance { get; protected set; }

        public PhysicalDevice PhysicalDevice { get; protected set; }

        public Surface Surface { get; protected set; }

        public Device Device { get; protected set; }

        public Queue GraphicsQueue { get; protected set; }

        //public virtual Queue PresentQueue { get; protected set; }

        public uint GraphicsFamily { get; protected set; }

        //public virtual uint PresentFamily { get; protected set; }

        public GRVkGetProcedureAddressDelegate GetProc { get; protected set; }

        public GRSharpVkGetProcedureAddressDelegate SharpVkGetProc { get; protected set; }

        public virtual void Dispose()
        {
            // dispose all the disposable members
            Device?.Dispose();
            Surface?.Dispose();
            Instance?.Dispose();
            GC.SuppressFinalize(this);
        }

        protected uint FindGraphicsQueueFamily()
        {
            var queueFamilyProperties = PhysicalDevice.GetQueueFamilyProperties();

            //queueFamilyProperties[0].
            var graphicsFamilies = queueFamilyProperties
                .Select((properties, index) => new { properties, index })
                .Where(pair => pair.properties.QueueFlags.HasFlag(QueueFlags.Graphics))
                .ToList();
            if (graphicsFamilies.Count == 0)
            {
                throw new Exception("Unable to find graphics queue");
            }

            Console.WriteLine($"Found {graphicsFamilies.Count} graphics families");
            foreach (var property in queueFamilyProperties)
            {
                Console.WriteLine(
                    $"{property.QueueFlags:G}, Queues: {property.QueueCount}, Min image size: {property.MinImageTransferGranularity.Height}x{property.MinImageTransferGranularity.Width} by {property.MinImageTransferGranularity.Depth}");
            }

            return (uint)graphicsFamilies[0].index;
        }
    }
}
