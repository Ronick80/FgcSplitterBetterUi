using System.ComponentModel;
using System.Diagnostics;
using System.IO;

namespace FgcKeyboardSplitter.Services;

public enum SetupOutcome
{
    Finished,
    RestartPending,
    Failed,
    Cancelled,
    ToolMissing,
}

/// <summary>Runs FgcDriverSetup.exe (shipped next to this program) with administrator rights.</summary>
public static class DriverSetupLauncher
{
    // Exit codes of FgcDriverSetup: 0 done, 1 failed, 2 wrong arguments, 3 done but a restart is pending.
    private const int RestartPendingExitCode = 3;
    private const int UserCancelledElevation = 1223;

    public static string ToolPath => Path.Combine(AppContext.BaseDirectory, "FgcDriverSetup.exe");

    public static bool IsAvailable => File.Exists(ToolPath);

    public static async Task<SetupOutcome> RunAsync()
    {
        if (!IsAvailable)
        {
            return SetupOutcome.ToolMissing;
        }

        var info = new ProcessStartInfo(ToolPath)
        {
            UseShellExecute = true,
            Verb = "runas",
            WorkingDirectory = AppContext.BaseDirectory,
        };

        try
        {
            using Process? process = Process.Start(info);
            if (process is null)
            {
                return SetupOutcome.Failed;
            }

            await process.WaitForExitAsync();
            return process.ExitCode switch
            {
                0 => SetupOutcome.Finished,
                RestartPendingExitCode => SetupOutcome.RestartPending,
                _ => SetupOutcome.Failed,
            };
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == UserCancelledElevation)
        {
            return SetupOutcome.Cancelled;
        }
        catch (Win32Exception)
        {
            return SetupOutcome.Failed;
        }
    }
}
