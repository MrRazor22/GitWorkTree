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
        private static int _commandCounter = 0;
        private readonly ILoggingService _loggingService;

        public GitCommandExecutor(ILoggingService loggingService)
        {
            _loggingService = loggingService;
        }

        private async Task LogMessageAsync(ILoggingService outputWindow, string message, bool showOutputPane = false)
        {
            if (outputWindow != null)
            {
                await outputWindow.WriteToOutputWindowAsync(message, showOutputPane);
            }
        }

        public async Task<bool> ExecuteAsync(string gitPath, string arguments, string workingDirectory, Action<string> outputHandler, System.Threading.CancellationToken cancellationToken = default)
        {
            int cmdId = System.Threading.Interlocked.Increment(ref _commandCounter);
            string logPrefix = $"[Cmd #{cmdId}]";
            ILoggingService outputWindow = _loggingService ?? LoggingHelper.Instance;

            if (string.IsNullOrEmpty(workingDirectory))
            {
                await LogMessageAsync(outputWindow, $"{logPrefix} The working directory is invalid or not loaded yet");
                return false;
            }

            if (!File.Exists(gitPath))
            {
                await LogMessageAsync(outputWindow, $"{logPrefix} Git executable not found at: {gitPath}", true);
                return false;
            }

            await LogMessageAsync(outputWindow, $"{logPrefix} Executing Git command: {arguments}", false);
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

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
                        _ = LogMessageAsync(outputWindow, $"{logPrefix} {e.Data}");
                    }
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                using var registration = cancellationToken.Register(() =>
                {
                    try
                    {
                        if (!process.HasExited)
                        {
                            process.Kill();
                        }
                    }
                    catch { }
                });

                await process.WaitForExitAsync(cancellationToken);
                stopwatch.Stop();

                bool isError = process.ExitCode != 0;

                if (isError && errorBuilder.Length == 0)
                {
                    await LogMessageAsync(outputWindow, $"{logPrefix} Git failed but no error output captured.", true);
                }

                var status = isError ? "Failed" : "Completed";
                await LogMessageAsync(outputWindow, $"{logPrefix} {status} (ExitCode={process.ExitCode}, {stopwatch.ElapsedMilliseconds}ms): git {arguments}");

                return !isError;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                await LogMessageAsync(outputWindow, $"{logPrefix} Failed (Exception after {stopwatch.ElapsedMilliseconds}ms during git {arguments}): {ex.Message}", true);
                return false;
            }
        }

        public async Task<GitCommandExecutionResult> ExecuteWithResultAsync(string gitPath, string arguments, string workingDirectory, Action<string> outputHandler = null, System.Threading.CancellationToken cancellationToken = default)
        {
            int cmdId = System.Threading.Interlocked.Increment(ref _commandCounter);
            string logPrefix = $"[Cmd #{cmdId}]";
            ILoggingService outputWindow = _loggingService ?? LoggingHelper.Instance;

            if (string.IsNullOrEmpty(workingDirectory))
            {
                await LogMessageAsync(outputWindow, $"{logPrefix} The working directory is invalid or not loaded yet");
                return new GitCommandExecutionResult(false, "The working directory is invalid or not loaded yet");
            }

            if (!File.Exists(gitPath))
            {
                await LogMessageAsync(outputWindow, $"{logPrefix} Git executable not found at: {gitPath}", true);
                return new GitCommandExecutionResult(false, $"Git executable not found at: {gitPath}");
            }

            await LogMessageAsync(outputWindow, $"{logPrefix} Executing Git command: {arguments}", false);
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

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
                        _ = LogMessageAsync(outputWindow, $"{logPrefix} {e.Data}");
                    }
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                using var registration = cancellationToken.Register(() =>
                {
                    try
                    {
                        if (!process.HasExited)
                        {
                            process.Kill();
                        }
                    }
                    catch { }
                });

                await process.WaitForExitAsync(cancellationToken);
                stopwatch.Stop();

                bool isError = process.ExitCode != 0;

                if (isError && errorBuilder.Length == 0)
                {
                    await LogMessageAsync(outputWindow, $"{logPrefix} Git failed but no error output captured.", true);
                }

                var status = isError ? "Failed" : "Completed";
                await LogMessageAsync(outputWindow, $"{logPrefix} {status} (ExitCode={process.ExitCode}, {stopwatch.ElapsedMilliseconds}ms): git {arguments}");

                return new GitCommandExecutionResult(!isError, errorBuilder.ToString().Trim());
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                await LogMessageAsync(outputWindow, $"{logPrefix} Failed (Exception after {stopwatch.ElapsedMilliseconds}ms during git {arguments}): {ex.Message}", true);
                return new GitCommandExecutionResult(false, ex.Message);
            }
        }
    }
}
