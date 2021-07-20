
namespace CPUAffinityOptimisationService
{
    partial class ProjectInstaller
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary> 
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Component Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.srvMainProcessInstaller = new System.ServiceProcess.ServiceProcessInstaller();
            this.srvMainInstaller = new System.ServiceProcess.ServiceInstaller();
            // 
            // srvMainProcessInstaller
            // 
            this.srvMainProcessInstaller.Account = System.ServiceProcess.ServiceAccount.LocalSystem;
            this.srvMainProcessInstaller.Password = null;
            this.srvMainProcessInstaller.Username = null;
            // 
            // srvMainInstaller
            // 
            this.srvMainInstaller.DisplayName = "CPU Affinity Optimisation Service";
            this.srvMainInstaller.ServiceName = "CPU Affinity Optimisation Service";
            this.srvMainInstaller.StartType = System.ServiceProcess.ServiceStartMode.Automatic;
            // 
            // ProjectInstaller
            // 
            this.Installers.AddRange(new System.Configuration.Install.Installer[] {
            this.srvMainProcessInstaller,
            this.srvMainInstaller});

        }

        #endregion

        private System.ServiceProcess.ServiceProcessInstaller srvMainProcessInstaller;
        private System.ServiceProcess.ServiceInstaller srvMainInstaller;
    }
}