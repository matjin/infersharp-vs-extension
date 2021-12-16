using infersharp_vs_ext.Commands;
using Microsoft.Sarif.Viewer.Interop;
using Microsoft.VisualStudio.Shell.Interop;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace infersharp_vs_ext
{
    [Command(PackageIds.MyCommand)]
    internal sealed class MyCommand : BaseCommand<MyCommand>
    {
        private static int InstalledExitCode;

        public static void RunCommands(Process p, string[] commands, OutputWindowPane pane, bool streamOutput = true)
        {
            var info = new ProcessStartInfo
            {
                Arguments = string.Join(";", commands),
                CreateNoWindow = true,
                FileName = "powershell.exe",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            if (streamOutput)
            {
                p.ErrorDataReceived += p_OutputDataReceived;
                p.OutputDataReceived += p_OutputDataReceived;
            }
            p.StartInfo = info;
            p.Start();
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();
            void p_OutputDataReceived(object sender, DataReceivedEventArgs e)
            {
                pane.WriteLine(e.Data);
            }
        }
        
        private string PrepareInputPath(string selectedPath)
        {
            var drivePrefix = selectedPath.Split('\\')[0];
            var newDrivePrefix = drivePrefix.Replace(":", string.Empty).ToLower();
            return "//mnt/" + newDrivePrefix + "/" + selectedPath.Substring(drivePrefix.Length + 1).Replace('\\', '/');
        }


        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            var resetEvent = new ManualResetEvent(false);
            void callBackCheck(object state)
            {
                using Process checkInstall = new();
                string[] checkInstallCommand = { Utils.RunWsl("ls " + Utils.INFERSHARP_FOLDER) };
                RunCommands(checkInstall, checkInstallCommand, infersharp_vs_extPackage.Pane, false);
                checkInstall.WaitForExit();
                InstalledExitCode = checkInstall.ExitCode;
                resetEvent.Set();
            }
            _ = ThreadPool.QueueUserWorkItem(callBack: callBackCheck);
            resetEvent.WaitOne();

            await infersharp_vs_extPackage.Pane.ClearAsync();
            await infersharp_vs_extPackage.Pane.ActivateAsync();
            if (InstalledExitCode != 0)
            {
                await infersharp_vs_extPackage.Pane.WriteLineAsync(
                    "Expected binaries not detected; please wait while they are downloaded and extracted.");
                using Process install = new();
                string[] installCommands =
                {
                    Utils.INSTALL_WSL_UBUNTU,
                    Utils.TRY_GET_INFERSHARP_BINARIES,
                    Utils.RunWslUbuntu("tar -xvzf " + Utils.INFERSHARP_TAR_GZ),
                    Utils.RunWslUbuntu("mv infersharp " + Utils.INFERSHARP_FOLDER_NAME),
                    Utils.RunWslUbuntu(Utils.Remove(Utils.INFERSHARP_TAR_GZ)),
                    Utils.SET_WSL_DEFAULT_UBUNTU,
                    Utils.Print("Setup complete. You may now run InferSharp!")
                };
                RunCommands(install, installCommands, infersharp_vs_extPackage.Pane);
            }
            else
            {
                using FolderBrowserDialog dialog = new();
                dialog.ShowNewFolderButton = false;
                dialog.Description =
                    "Please select the root of the directory tree containing the binaries you want to analyze.";
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    var inputPath = PrepareInputPath(dialog.SelectedPath);
                    await infersharp_vs_extPackage.Pane.WriteLineAsync("InferSharp is analyzing: " + inputPath);
                    async void callBack(object state)
                    {
                        using Process p = new();
                        string[] analysisCommands =
                        {
                            Utils.RunWsl(Utils.Remove(Utils.INFER_OUT)),
                            Utils.Print("Beginning translation."),
                            Utils.TranslateAndMove(inputPath),
                            Utils.Print("Translation complete. Beginning analysis."),
                            Utils.InferAnalyze(inputPath)
                        };
                        RunCommands(p, analysisCommands, infersharp_vs_extPackage.Pane);
                        p.WaitForExit();
                        var sarifReportPath = dialog.SelectedPath + "\\infer-out\\report.sarif";
                        if (File.Exists(sarifReportPath))
                        {
                            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                            var shell = Microsoft.VisualStudio.Shell.Package.GetGlobalService(typeof(SVsShell)) as IVsShell;
                            var viewer = new SarifViewerInterop(shell);
                            await viewer.OpenSarifLogAsync(sarifReportPath);
                        }
                    }
                    _ = ThreadPool.QueueUserWorkItem(callBack: callBack);
                }
                else
                {
                    await infersharp_vs_extPackage.Pane.WriteLineAsync("No valid folder given.");
                }

            }
        }
    }
}
