global using Community.VisualStudio.Toolkit;
global using Microsoft.VisualStudio.Shell;
global using System;
global using Task = System.Threading.Tasks.Task;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using infersharp_vs_ext.Commands;

namespace infersharp_vs_ext
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration(Vsix.Name, Vsix.Description, Vsix.Version)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [Guid(PackageGuids.infersharp_vs_extString)]
    public sealed class infersharp_vs_extPackage : ToolkitPackage
    {
        public static OutputWindowPane Pane;

        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            Pane = await VS.Windows.CreateOutputWindowPaneAsync("InferSharp");

            await this.RegisterCommandsAsync();
        }
    }
}