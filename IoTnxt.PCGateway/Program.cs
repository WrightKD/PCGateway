using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;
using Gma.UserActivityMonitor;
using IoTnxt.DAPI.RedGreenQueue.Adapter;
using IoTnxt.DAPI.RedGreenQueue.Extensions;
using IoTnxt.DAPI.RedGreenQueue.Proxy;
using IoTnxt.Gateway.API.Abstractions;
using IoTnxt.PCGateway;
using IoTnxt.RedGreenQueue;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Mono.Options;

namespace IoTnxt.Example.Gateway
{
    public class Program
    {
        //GatewayId needs to unique identify your gateway (service)
        //A gateway only connects to one IoT.nxt tenant - provision a separate gateway for each different IoT.nxt tenant
        public static string GatewayId = "PCGateway_" + (
            from nic in NetworkInterface.GetAllNetworkInterfaces()
            where nic.OperationalStatus == OperationalStatus.Up
            select nic.GetPhysicalAddress().ToString()
        ).FirstOrDefault();

        //Secret key is your password.  It should be strong and secure and never shared except during registration.
        //It cannot be changed once you have registered the gateway.
        //Replace this with a strong password!
        public static string SecretKey = Convert.ToBase64String(MD5.Create().ComputeHash(Encoding.UTF8.GetBytes(GatewayId)));

        public static DateTime LastKeystroke = DateTime.Now;

        private static void Main(string[] args)
        {
            HookManager.KeyDown += (s, e) => LastKeystroke = DateTime.Now;

            Console.WriteLine($"Gateway Id: {GatewayId}");

            var services = new ServiceCollection();

            services.AddOptions();
            services.AddLogging(lb => lb.AddConsole().SetMinimumLevel(LogLevel.Trace));
            services.AddDapiRedGreenQueue(new ConfigurationBuilder().Build());
            services.AddDapiRedGreenQueueProxy().AddSingletonProxy<IGatewayApi>();
            services.AddSingleton<PCGateway.PcGateway>();
            services.AddSingleton<WindowsServiceManager>();

            services.Configure<RedGreenQueueAdapterOptions>(cfg =>
            {
                cfg.LogSends = true;
                cfg.GreenQueueOptions = new GreenQueueOptions
                {
                    Hosts = new List<string> { "greenqueue.prod.iotnxt.io" },
                    ServiceUniqueId = GatewayId,
                    SecretKey = SecretKey,
                    publicKeyAsXml =
                        "<RSAKeyValue><Exponent>AQAB</Exponent><Modulus>rbltknM3wO5/TAEigft0RDlI6R9yPttweDXtmXjmpxwcuVtqJgNbIQ3VduGVlG6sOg20iEbBWMCdwJ3HZTrtn7qpXRdJBqDUNye4Xbwp1Dp+zMFpgEsCklM7c6/iIq14nymhNo9Cn3eBBM3yZzUKJuPn9CTZSOtCenSbae9X9bnHSW2qB1qRSQ2M03VppBYAyMjZvP1wSDVNuvCtjU2Lg/8o/t231E/U+s1Jk0IvdD6rLdoi91c3Bmp00rVMPxOjvKmOjgPfE5LESRPMlUli4kJFWxBwbXuhFY+TK2I+BUpiYYKX+4YL3OFrn/EpO4bNcI0NHelbWGqZg57x7rNe9Q==</Modulus></RSAKeyValue>"
                };
            });
            services.Configure<DapiRedGreenQueueProxyOptions>(cfg => cfg.Partition = "IOTNXT.DEFAULT");

            var sp = services.BuildServiceProvider();

            var mode = "run";
            var serviceName = "IoTnxtPCGateway";
            var displayName = "IoT.nxt PC Gateway";
            var description = "A PC connector for the IoT.nxt platform";

            var opts = new OptionSet
            {
                { "mode=", "install|uninstall|start|stop|run", s => mode = s },
                { "serviceName=", "The name of the Windows service", s => serviceName = s },
                { "displayName=", "The display name of the Windows service", s => displayName = s },
                { "description=", "The description of the service", s => description = s }
            };

            opts.Parse(args);

            var sm = sp.GetService<WindowsServiceManager>();
            sm.DoRun = () => sp.GetService<PcGateway>();
            sm.Run(mode, serviceName, displayName, description);

            Application.Run();
        }
    }
}
