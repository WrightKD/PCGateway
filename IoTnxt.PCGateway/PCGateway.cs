using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using IoTnxt.Common.Extensions;
using IoTnxt.DAPI.RedGreenQueue.Abstractions;
using IoTnxt.DAPI.RedGreenQueue.Adapter;
using IoTnxt.Example.Gateway;
using IoTnxt.Gateway.API.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic.Devices;
using Newtonsoft.Json.Linq;

namespace IoTnxt.PCGateway
{
    public class PcGateway
    {
        private readonly IGatewayApi _gatewayApi;
        private readonly ILogger<PcGateway> _logger;
        private readonly IRedGreenQueueAdapter _redq;

        private readonly PerformanceCounter _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
        private readonly PerformanceCounter _ramCounter = new PerformanceCounter("Memory", "Available MBytes");

        private async Task RunAsync()
        {
            try
            {
                var gw = new Gateway.API.Abstractions.Gateway
                {
                    GatewayId = Program.GatewayId,
                    Secret = Program.SecretKey,
                    Make = "IoT.nxt",
                    Model = "PC Gateway",
                    FirmwareVersion = "1.0.0",
                    Devices = new Dictionary<string, Device>
                    {
                        ["PC"] = new Device
                        {
                            DeviceName = "PC",
                            DeviceType = "Personal Computer",
                            Properties = new Dictionary<string, DeviceProperty>
                            {
                                ["MachineName"] = new DeviceProperty { PropertyName = "MachineName" },
                                ["ProcessorCount"] = new DeviceProperty { PropertyName = "ProcessorCount" },
                                ["OSVersion"] = new DeviceProperty { PropertyName = "OSVersion" },
                                ["ProcessCount"] = new DeviceProperty { PropertyName = "ProcessCount" },
                                ["CursorPosition"] = new DeviceProperty { PropertyName = "CursorPosition" },
                                ["TotalMemoryGB"] = new DeviceProperty { PropertyName = "TotalMemoryGB" },
                                ["SecondsIdle"] = new DeviceProperty { PropertyName = "SecondsIdle" },
                                ["CPUUsage"] = new DeviceProperty { PropertyName = "CPUUsage" },
                                ["AvailableRAMGB"] = new DeviceProperty { PropertyName = "AvailableRAMGB" },
                                ["Execute"] = new DeviceProperty { PropertyName = "Execute" }
                            }
                        },
                        ["Apps"] = new Device
                        {
                            DeviceName = "Apps",
                            DeviceType = "Applications",
                            Properties = new Dictionary<string, DeviceProperty>
                            {
                                ["SkypeOpen"] = new DeviceProperty { PropertyName = "SkypeOpen" },
                                ["Notepads"] = new DeviceProperty { PropertyName = "Notepads" },
                                ["Chromes"] = new DeviceProperty { PropertyName = "Chromes" },
                                ["VisualStudios"] = new DeviceProperty { PropertyName = "VisualStudios" },
                            }
                        }

                    }
                };

                await _gatewayApi.RegisterGatewayFromGatewayAsync(gw);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initialization example gateway");
            }


            string tenantId = "t000000019";
            string lastCursorPosition = null;
            var intervalMs = 1000;
            var lastCursorMove = DateTime.Now;

            await _redq.SubscribeAsync($"GATEWAY.1.*.{Program.GatewayId}.REQ",
                "requests", null,
                queueName: Program.GatewayId,
                createQueue: true,
                process: async (q, jo, a, b, c, headers) =>
                {
                    var command = jo["deviceGroups"]["PC"].Value<string>("Execute");
                    var parts = command.Split("|");
                    var path = parts.Length > 0 ? parts[0] : "";
                    var arguments = parts.Length > 1 ? parts[1] : "";

                    try
                    {
                        new Process
                        {
                            StartInfo = {
                                FileName = path,
                                Arguments = arguments
                            }
                        }.Start();

                        //Echo the command to confirm execution
                        await _redq.SendGateway1NotificationAsync(
                            tenantId,
                            Program.GatewayId,
                            DateTime.UtcNow,
                            null,
                            null,
                            DateTime.UtcNow,
                            true,
                            false,
                            ("PC", "Execute", command)
                        );
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Processing command: {command}");
                    }

                    return true;
                });

            while (true)
            {
                try
                {
                    var processes = Process.GetProcesses();

                    var machineName = Environment.MachineName;
                    var processorCount = Environment.ProcessorCount;
                    var osVersion = Environment.OSVersion;
                    var processCount = processes.Length;
                    var skypeOpen = processes.Any(p => p.ProcessName.ContainsAnyOfNoCase("Skype"));
                    var notepadsOpen = processes.Count(p => p.ProcessName.ContainsAnyOfNoCase("Notepad"));
                    var chromesOpen = processes.Count(p => p.ProcessName.ContainsAnyOfNoCase("Chrome"));
                    var visualStudiosOpen = processes.Count(p => p.ProcessName.ContainsAnyOfNoCase("devenv"));
                    var cursorPosition = $"{Cursor.Position.X},{Cursor.Position.Y}";
                    var cpuUsage = _cpuCounter.NextValue();
                    var avalableRamGb = _ramCounter.NextValue() / 1024.0;

                    var computerInfo = new ComputerInfo();
                    var totalMemoryGb = computerInfo.TotalPhysicalMemory / (1024 * 1024 * 1024);

                    if (cursorPosition != lastCursorPosition)
                    {
                        lastCursorPosition = cursorPosition;
                        lastCursorMove = DateTime.Now;
                    }

                    var lastActivity = lastCursorMove > Program.LastKeystroke ? lastCursorMove : Program.LastKeystroke;
                    var secondsIdle = (DateTime.Now - lastActivity).TotalSeconds;

                    await _redq.SendGateway1NotificationAsync(
                        tenantId,
                        Program.GatewayId,
                        DateTime.UtcNow,
                        null,
                        null,
                        DateTime.UtcNow,
                        true,
                        false,
                        ("PC", "MachineName", machineName),
                        ("PC", "ProcessorCount", processorCount),
                        ("PC", "OSVersion", osVersion.ToString()),
                        ("PC", "ProcessCount", processCount),
                        ("PC", "CursorPosition", cursorPosition),
                        ("PC", "TotalMemoryGB", totalMemoryGb),
                        ("PC", "SecondsIdle", secondsIdle),
                        ("PC", "CPUUsage", cpuUsage),
                        ("PC", "AvailableRAMGB", avalableRamGb),
                        ("Apps", "SkypeOpen", skypeOpen),
                        ("Apps", "Notepads", notepadsOpen),
                        ("Apps", "Chromes", chromesOpen),
                        ("Apps", "VisualStudios", visualStudiosOpen)
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error sending telemetry");
                }

                await Task.Delay(intervalMs);
            }
        }

        public PcGateway(
            IRedGreenQueueAdapter redq,
            ILogger<PcGateway> logger,
            IGatewayApi gatewayApi)
        {
            _gatewayApi = gatewayApi ?? throw new ArgumentNullException(nameof(gatewayApi));
            _redq = redq ?? throw new ArgumentNullException(nameof(redq));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            Task.Run(RunAsync);
        }
    }
}
