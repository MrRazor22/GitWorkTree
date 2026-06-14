using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Threading;

namespace GitWorkTree.Services
{
    public class GitCommandExecutor : IGitCommandExecutor
    {
        private readonly ILoggingService _loggingService;

        public GitCommandExecutor(ILoggingService loggingService)
        {
            _loggingService = loggingService;
        }

        public async Task<bool> ExecuteAsync(string gitPath, string arguments, string workingDirectory, Action<string> outputHandler)
        {
            ILoggingService outputWindow = _loggingService ?? LoggingHelper.Instance;

            if (string.IsNullOrEmpty(workingDirectory))
            {
                outputWindow?.WriteToOutputWindowAsync("The working directory is invalid or not loaded yet");
                return false;
            }

            if (!File.Exists(gitPath))
            {
                outputWindow?.WriteToOutputWindowAsync($"Git executable not found at: {gitPath}", true);
                return false;
            }

            outputWindow?.WriteToOutputWindowAsync($"Executing Git command: {arguments}", true);

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = gitPath,
                    Arguments = arguments,
                    WorkingDirectory = workingDirectory,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true
                };

                using var process = new Process { StartInfo = startInfo };
                var outputBuilder = new StringBuilder();
                var errorBuilder = new StringBuilder();

                process.OutputDataReceived += (s, e) =>
                {
                    if (e.Data != null)
                    {
                        outputBuilder.AppendLine(e.Data);
                        outputHandler?.Invoke(e.Data);
                    }
                };

                process.ErrorDataReceived += (s, e) =>
                {
                    if (e.Data != null)
                    {
                        errorBuilder.AppendLine(e.Data);
                        outputWindow?.WriteToOutputWindowAsync(e.Data).Forget();
                    }
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                await process.WaitForExitAsync();

                bool isError = process.ExitCode != 0;

                if (isError && errorBuilder.Length == 0)
                    outputWindow?.WriteToOutputWindowAsync("Git failed but no error output captured.", true);

                var result = isError ? "failed" : "completed";
                outputWindow?.WriteToOutputWindowAsync($"Command execution - {result}");

                return !isError;
            }
            catch (Exception ex)
            {
                outputWindow?.WriteToOutputWindowAsync($"An error occurred during Git command execution: {ex.Message}", true);
                return false;
            }
        }
    }
}
