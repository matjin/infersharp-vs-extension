using infersharp_vs_ext.Commands;
using Microsoft.Sarif.Viewer.Interop;
using Microsoft.VisualStudio.Shell.Interop;
using System.Diagnostics;
using System.Windows.Forms;

namespace infersharp_vs_ext
{
    [Command(PackageIds.MyCommand)]
    internal sealed class MyCommand : BaseCommand<MyCommand>
    {
        private void RunCommands(Process p, string[] commands, OutputWindowPane pane, string selectedPath)
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
            p.ErrorDataReceived += p_OutputDataReceived;
            p.OutputDataReceived += p_OutputDataReceived;
            p.StartInfo = info;
            p.Start();
            p.EnableRaisingEvents = true;
            //p.Exited += new EventHandler(p_ProcessExited);
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();
            void p_OutputDataReceived(object sender, DataReceivedEventArgs e)
            {
                pane.WriteLine(e.Data);
            }
            async void p_ProcessExited(object sender, EventArgs e)
            {
                await pane.WriteLineAsync("InferSharp analysis completed.");
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                var vsShell = (IVsShell)ServiceProvider.GlobalProvider.GetService(typeof(IVsShell));
                var viewer = new SarifViewerInterop(vsShell);
                await viewer.OpenSarifLogAsync(selectedPath + "\\infer-out\\report.sarif");
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
            OutputWindowPane pane = await VS.Windows.CreateOutputWindowPaneAsync("InferSharp");
            using Process p = new Process();
            using FolderBrowserDialog dialog = new();
            dialog.ShowNewFolderButton = false;
            dialog.Description =
                "Please select the root of the directory tree containing the binaries you want to analyze.";
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                var inputPath = PrepareInputPath(dialog.SelectedPath);
                await pane.WriteLineAsync("InferSharp is analyzing: " + inputPath);
                string[] analysisCommands = {
                    Utils.RunWsl(Utils.Remove(Utils.INFER_OUT)),
                    Utils.Print("Beginning translation."),
                    Utils.TranslateAndMove(inputPath),
                    Utils.Print("Translation complete. Beginning analysis."),
                    Utils.InferAnalyze(inputPath)
                };
                RunCommands(p,analysisCommands, pane, dialog.SelectedPath);
              //  p.WaitForExit();
            }
            else
            {
                await pane.WriteLineAsync("No valid folder given.");
            }
        }
    }
}
