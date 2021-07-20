
using System.ComponentModel;

namespace CPUAffinityOptimisationService
{
    [RunInstaller(true)]
    public partial class ProjectInstaller : System.Configuration.Install.Installer
    {

        public ProjectInstaller()
        {
            InitializeComponent();

            // Set install parameters
            this.srvMainInstaller.Description = "Optimises CPU affinity for all instances of " + srvMain.strApplicationName;
            this.srvMainInstaller.ServiceName = srvMain.strApplicationName + " CPU Affinity Optimiser";
            this.srvMainInstaller.DisplayName = srvMain.strApplicationName + " CPU Affinity Optimiser";

        }

    }
}
