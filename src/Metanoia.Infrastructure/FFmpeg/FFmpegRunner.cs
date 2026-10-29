using System.Diagnostics;
using Metanoia.Core.Contracts;
using Metanoia.Core.Models;
using Metanoia.Core.Services;

namespace Metanoia.Infrastructure.FFmpeg;

public sealed class FFmpegRunner : IFFmpegRunner
{
    private readonly FFmpegProgressParser _progressParser;

    public FFmpegRunner(FFmpegProgressParser progressParser)
    {
        _progressParser = progressParser ?? throw new ArgumentNullException(nameof(progressParser));
    }

    public async Task<FFmpegRunResult> RunAsync(
        FFmpegCommand command,
        TimeSpan? expectedDuration,
        IProgress<JobProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        FFmpegArgumentValidator.ValidateExecutable(command.Executable);
        FFmpegArgumentValidator.ValidateArguments(command.Arguments);

        var startInfo = new ProcessStartInfo
        {
            FileName = command.Executable,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        foreach (var argument in command.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
        {
            throw new InvalidOperationException("No fue posible iniciar FFmpeg.");
        }

        var progressTask = ReadProgressAsync(process.StandardOutput, expectedDuration, progress);
        var errorTask = process.StandardError.ReadToEndAsync(CancellationToken.None);
        var wasCancelled = false;

        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            wasCancelled = true;
            TryKill(process);
            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
        }

        await progressTask.ConfigureAwait(false);
        var standardError = await errorTask.ConfigureAwait(false);

        return new FFmpegRunResult(
            !wasCancelled && process.ExitCode == 0,
            wasCancelled,
            process.ExitCode,
            standardError);
    }

    private async Task ReadProgressAsync(
        StreamReader reader,
        TimeSpan? expectedDuration,
        IProgress<JobProgress>? progress)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);

        while (await reader.ReadLineAsync().ConfigureAwait(false) is { } line)
        {
            var separatorIndex = line.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = line[..separatorIndex];
            var value = line[(separatorIndex + 1)..];
            values[key] = value;

            if (!string.Equals(key, "progress", StringComparison.Ordinal))
            {
                continue;
            }

            progress?.Report(_progressParser.Parse(values, expectedDuration));
            values.Clear();
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // El proceso ya terminó.
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // El sistema ya estaba cerrando el proceso; WaitForExit confirma el estado.
        }
    }
}
