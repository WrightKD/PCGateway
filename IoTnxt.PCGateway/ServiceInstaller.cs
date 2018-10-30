using System.Collections;
using System.ComponentModel;
using System.Configuration.Install;
using System.ServiceProcess;
using System.Text;

namespace IoTnxt.PCGateway
{
    /// <summary>
    /// Implementation of <see cref="System.Configuration.Install.Installer"/> to be used to install/uninstall services
    /// </summary>
    [RunInstaller(true)]
    public class ServiceInstaller : Installer
    {
        /// <summary>
        /// Default Constructor receiving service details
        /// </summary>
        /// <param name="displayName">The service name to be displayed in the services console</param>
        /// <param name="serviceName">The name used to perform any action on the service by Windows or other 3rd party applications</param>
        /// <param name="description">The service description displayed in the services console</param>
        /// <param name="installAccount">Account type to use when running as service</param>
        public ServiceInstaller(string displayName, string serviceName, string description, ServiceAccount installAccount = ServiceAccount.LocalSystem)
        {
            var serviceInstaller = new System.ServiceProcess.ServiceInstaller
            {
                DisplayName = displayName,
                ServiceName = serviceName,
                Description = description,
                DelayedAutoStart = true,
                StartType = ServiceStartMode.Automatic
                
            };

            Installers.Add(serviceInstaller);

            var processInstaller = new ServiceProcessInstaller
            {
                Account = installAccount
            };
            
            Installers.Add(processInstaller);
        }

        protected override void OnBeforeInstall(IDictionary savedState)
        {
            var path = new StringBuilder(Context.Parameters["assemblypath"]);
            if (path[0] != '"')
            {
                path.Insert(0, '"');
                path.Append('"');
            }
            path.Append(" /runas service");
            Context.Parameters["assemblypath"] = path.ToString();

            base.OnBeforeInstall(savedState);
        }
    }
}