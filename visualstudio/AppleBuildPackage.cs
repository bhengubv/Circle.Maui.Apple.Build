using System;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.VisualBasic;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

[PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
[ProvideMenuResource("Menus.ctmenu", 1)]
[Guid("320C2BD2-4F97-4A08-9E9D-93E1C64E8928")]
public sealed class AppleBuildPackage : AsyncPackage
{
    private static readonly Guid Commands = new Guid("CB6A6ADF-1854-45F7-9ECF-166D668A8193");

    protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
    {
        await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        if (await GetServiceAsync(typeof(IMenuCommandService)) is OleMenuCommandService menu)
        {
            menu.AddCommand(new MenuCommand((_, __) => AddPackage(), new CommandID(Commands, 0x0100)));
            menu.AddCommand(new MenuCommand((_, __) => InspectIpa(), new CommandID(Commands, 0x0101)));
        }
    }

    private static void AddPackage()
    {
        using (var dialog = new OpenFileDialog { Title = "Select the MAUI project", Filter = "MSBuild project (*.csproj)|*.csproj" })
        {
            if (dialog.ShowDialog() != DialogResult.OK) return;
            _ = RunAsync("dotnet", "add " + Quote(dialog.FileName) + " package Circle.Maui.Apple.Build --version 1.0.0 --no-restore", Path.GetDirectoryName(dialog.FileName));
        }
    }

    private static void InspectIpa()
    {
        using (var dialog = new OpenFileDialog { Title = "Select the IPA", Filter = "iOS IPA (*.ipa)|*.ipa" })
        {
            if (dialog.ShowDialog() != DialogResult.OK) return;
            var bundleId = Interaction.InputBox("Enter the expected Release bundle ID.", "Circle MAUI Apple Build").Trim();
            if (string.IsNullOrWhiteSpace(bundleId)) return;
            if (!System.Text.RegularExpressions.Regex.IsMatch(bundleId, @"^[A-Za-z0-9.-]+$"))
            {
                MessageBox.Show("Enter a valid bundle ID.", "Circle MAUI Apple Build");
                return;
            }

            var helper = Path.Combine(Path.GetDirectoryName(typeof(AppleBuildPackage).Assembly.Location), "tools", "xtool-maui-ipa.py");
            _ = RunAsync("py", "-3 " + Quote(helper) + " " + Quote(dialog.FileName) + " --bundle-id " + bundleId, Path.GetDirectoryName(dialog.FileName));
        }
    }

    private static string Quote(string value) => "\"" + value + "\"";

    private static async Task RunAsync(string executable, string args, string directory)
    {
        try
        {
            var result = await Task.Run(async () =>
            {
                using (var process = Process.Start(new ProcessStartInfo(executable, args)
                {
                    WorkingDirectory = directory,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }))
                {
                    var output = process.StandardOutput.ReadToEndAsync();
                    var error = process.StandardError.ReadToEndAsync();
                    process.WaitForExit();
                    return $"Exit code {process.ExitCode}\n\n{await output}\n{await error}";
                }
            });
            MessageBox.Show(result, "Circle MAUI Apple Build");
        }
        catch (Exception error)
        {
            MessageBox.Show(error.Message, "Circle MAUI Apple Build");
        }
    }
}
