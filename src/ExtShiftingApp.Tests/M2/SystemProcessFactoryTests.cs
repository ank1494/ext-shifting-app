using System.Diagnostics;
using ExtShiftingApp.M2;

namespace ExtShiftingApp.Tests.M2;

public class SystemProcessFactoryTests
{
    /// <summary>
    /// Script-mode processes must not allow sending input, even though stdin is now always
    /// redirected (so M2 gets EOF rather than blocking on an inherited terminal stdin).
    /// </summary>
    [Fact]
    public async Task ScriptModeProcess_SendInputAsync_ThrowsEvenWhenStdinIsRedirected()
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "cmd",
                Arguments = "/c exit 0",
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
            EnableRaisingEvents = true,
        };

        var runningProcess = new SystemRunningProcess(process, isInteractive: false);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => runningProcess.SendInputAsync("hello"));

        await runningProcess.WaitForExitAsync(CancellationToken.None);
    }
}
