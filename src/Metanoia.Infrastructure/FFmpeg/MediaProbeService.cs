using System.Diagnostics;
using Metanoia.Core.Contracts;
using Metanoia.Core.Models;
using Metanoia.Core.Services;

namespace Metanoia.Infrastructure.FFmpeg;

public sealed class MediaProbeService : IMediaProbeService
{
    private readonly string _ffprobePath;
    private readonly FfprobeJsonParser _parser;

    public MediaProbeService(string ffprobePath, FfprobeJsonParser parser)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ffprobePath);
        _ffprobePath = ffprobePath;
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
    }

    public async Task<MediaProbeResult> ProbeAsync(
        string inputPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputPath);
        var fullPath = Path.GetFullPath(inputPath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("No se encontró el archivo a analizar.", fullPath);
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = _ffprobePath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        foreach (var argument in new[]
                 {
                     "-v", "error",
                     "-print_format", "json",
                     "-show_format",
                     "-show_streams",
                     fullPath
                 })
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
        {
            throw new MediaProbeException("No fue posible iniciar FFprobe.", string.Empty);
        }

        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }

        var output = await outputTask.ConfigureAwait(false);
        var error = await errorTask.ConfigureAwait(false);

        if (process.ExitCode != 0)
        {
            throw new MediaProbeException(
                "FFprobe no pudo analizar el archivo seleccionado.",
                error.Trim());
        }

        return _parser.Parse(output, new FileInfo(fullPath));
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
            // El proceso ya terminó entre la comprobación y Kill.
        }
    }
}
