using System;
using System.Collections;
using System.Configuration.Install;
using System.Diagnostics;
using System.ServiceProcess;
using IoTnxt.Logging;
using Microsoft.Extensions.Logging;

namespace IoTnxt.PCGateway
{
    public class WindowsServiceManager : ServiceBase
    {
        private readonly ILogger _logger;

        public WindowsServiceManager(ILogger<WindowsServiceManager> logger)
        {
            _logger = logger;
        }

        private void DoStart(string serviceName)
        {
            if (string.IsNullOrWhiteSpace(serviceName))
            {
                _logger.LogWarning("Missing parameter : [/serviceName <value>]");
            }

            _logger.LogInformation("Starting Service : {0}", serviceName);
            var sc = new ServiceController(serviceName ?? throw new ArgumentNullException(nameof(serviceName)));
            sc.Start();
            _logger.LogInformation("Started Service : {0}", serviceName);
        }

        private void DoStop(string serviceName)
        {
            if (string.IsNullOrWhiteSpace(serviceName))
            {
                _logger.LogWarning("Missing parameter : [/serviceName <value>]");
            }

            _logger.LogInformation("Stopping Service : {0}", serviceName);
            var sc = new ServiceController(serviceName ?? throw new ArgumentNullException(nameof(serviceName)));
            sc.Stop();
            _logger.LogInformation("Stopping Service : {0}", serviceName);
        }
    

        private void DoInstall(string serviceName, string displayName, string description)
        {
            _logger.LogInformation("Install: Preparing");

            var isMissingParam = false;
            if (string.IsNullOrWhiteSpace(serviceName))
            {
                _logger.LogWarning("Missing parameter : [/serviceName <value>]");
                isMissingParam = true;
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                _logger.LogWarning("Missing parameter : [/displayName <value>]");
                isMissingParam = true;
            }

            if (string.IsNullOrWhiteSpace(description))
            {
                _logger.LogWarning("Missing parameter : [/description <value>]");
                isMissingParam = true;
            }

            if (isMissingParam)
            {
                _logger.LogError("Install : Aborting due to missing parameters");
                return;
            }


            var exeName = Process.GetCurrentProcess().MainModule.FileName;
            _logger.LogInformation($"Install: Parameters [serviceName:{serviceName}] [displayName:{displayName}] [description:{description}] [path:{exeName}]");

            

            var installer = new TransactedInstaller();
            var pi = new ServiceInstaller(displayName, serviceName, description);
            installer.Installers.Add(pi);
            var path = $"/assemblypath={exeName}";
            var context = new InstallContext("install.log", new[] { path });
            context.LogMessage("Using path " + path);
            installer.Context = context;
            var state = new Hashtable();
            

            try
            {
                installer.Install(state);
            }
            catch (Exception e)
            {
                _logger.LogExceptionEx(e, e.Message, ("serviceName", serviceName));
            }


            _logger.LogInformation("Install : Done");
        }

        private void DoExecute()
        {
            _logger.LogInformation("Execute: Preparing");
            Run(this);
            _logger.LogInformation("Execute: Stopping");            
        }

        public Action DoRun;

        protected override void OnStart(string[] args)
        {
            base.OnStart(args);
            DoRun?.Invoke();
        }

        private void DoUninstall(string serviceName)
        {
            _logger.LogInformation("Uninstall: Preparing");

            var isMissingParam = false;
            if (string.IsNullOrWhiteSpace(serviceName))
            {
                _logger.LogWarning("Missing parameter : [/serviceName <value>]");
                isMissingParam = true;
            }

            if (isMissingParam)
            {
                _logger.LogError("Uninstall : Aborting due to missing parameters");
                return;
            }

            var exeName = Process.GetCurrentProcess().MainModule.FileName;
            _logger.LogInformation($"Uninstall: Parameters [serviceName:{serviceName}] [path:{exeName}]");

            var installer = new TransactedInstaller();
            var pi = new ServiceInstaller(string.Empty, serviceName, string.Empty);
            installer.Installers.Add(pi);
            var path = $"/assemblypath={exeName}";
            var context = new InstallContext("install.log", new[] { path });
            context.LogMessage("Using path " + path);

            installer.Context = context;

            try
            {
                installer.Uninstall(null);
            }
            catch (Exception e)
            {
               _logger.LogExceptionEx(e, e.Message, ("serviceName",serviceName));
            }
            

            _logger.LogInformation("Uninstall : Done");
        }

        public void Run(string mode, string serviceName, string displayName, string description)
        {
            _logger.LogInformation("RunAs: Service: Running Application as Windows Service");
            var serviceMode = mode;
            _logger.LogInformation($"Service Mode : [{serviceMode}]");

            switch (serviceMode.ToLower())
            {
                case "install":
                {
                    DoInstall(serviceName, displayName, description);
                    break;
                }

                case "uninstall":
                {
                    DoUninstall(serviceName);
                    break;
                }

                case "start":
                {
                    DoStart(serviceName);
                    break;
                }
                case "stop":
                {
                    DoStop(serviceName);
                    break;
                }

                default:
                {
                    if (Environment.UserInteractive)
                        DoRun?.Invoke();
                    else
                        DoExecute();

                    break;
                }
            }
        }
    }
}