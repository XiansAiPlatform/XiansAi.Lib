using System;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Text.Json;
using System.Text;

public class SystemProcess
{
    public async Task<string> RunCommandAsync(string command, string arguments = "")
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = command, 
            Arguments = arguments, 
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using (var process = new Process { StartInfo = startInfo })
        {
            var output = new StringBuilder();
            var error = new StringBuilder();

            process.OutputDataReceived += (sender, args) => output.AppendLine(args.Data);
            process.ErrorDataReceived += (sender, args) => error.AppendLine(args.Data);

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                throw new Exception($"Docker command failed with exit code {process.ExitCode}: {error}");
            }

            return output.ToString();
        }
    }

}