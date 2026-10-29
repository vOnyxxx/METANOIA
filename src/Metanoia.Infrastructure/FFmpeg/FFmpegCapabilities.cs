using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Metanoia.Infrastructure.FFmpeg;

public sealed record ExportContainer(string Name, string Description, string Extension, bool Audio, bool Video)
{
    public string Label => $"{Extension.ToUpperInvariant()} · {Description}";
}

/// <summary>Discovers the running build, rather than assuming support from a fixed format list.</summary>
public sealed class FFmpegCapabilities(string executable)
{
    public async Task<IReadOnlyList<ExportContainer>> DiscoverContainersAsync(CancellationToken token = default)
    {
        var muxers = await QueryAsync(["-muxers"], token);
        var names = Regex.Matches(muxers, @"(?m)^\s+E\s+(\w+)\s+(.+)$")
            .Select(m => (Name: m.Groups[1].Value, Description: m.Groups[2].Value.Trim())).ToArray();
        using var gate = new SemaphoreSlim(4);
        var results = await Task.WhenAll(names.Select(async item =>
        {
            await gate.WaitAsync(token);
            try
            {
                var help = await QueryAsync(["-h", "muxer=" + item.Name], token);
                var extension = Regex.Match(help, @"Common extensions: ([a-zA-Z0-9]+)").Groups[1].Value;
                var audio = help.Contains("Default audio codec:"); var video = help.Contains("Default video codec:");
                if (extension.Length == 0 || !audio && !video || item.Name is "hls" or "dash" or "segment" or "stream_segment" or "tee") return null;
                return new ExportContainer(item.Name, item.Description, extension, audio, video);
            }
            finally { gate.Release(); }
        }));
        var containers = results.OfType<ExportContainer>().ToList();
        var imageHelp = await QueryAsync(["-h", "muxer=image2"], token);
        var extensions = Regex.Match(imageHelp, @"Common extensions: ([^\r\n]+)").Groups[1].Value.TrimEnd('.').Split(',');
        containers.RemoveAll(c => c.Name == "image2");
        foreach (var extension in extensions.Where(e => Regex.IsMatch(e, "^[a-zA-Z0-9]+$")))
            containers.Add(new ExportContainer("image2", "Imagen " + extension.ToUpperInvariant(), extension, false, true));
        return containers.DistinctBy(c => (c.Name, c.Extension)).OrderBy(x => x.Extension).ToArray();
    }

    public async Task<string> DiscoverFiltersAsync(CancellationToken token = default) => await QueryAsync(["-filters"], token);
    public async Task<string> GetFilterHelpAsync(string name, CancellationToken token = default)
    {
        if (!Regex.IsMatch(name, "^[a-zA-Z0-9_]+$")) throw new ArgumentException("Filtro inválido.");
        return await QueryAsync(["-h", "filter=" + name], token);
    }

    private async Task<string> QueryAsync(string[] arguments, CancellationToken token)
    {
        var info = new ProcessStartInfo(executable) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        info.ArgumentList.Add("-hide_banner"); foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = Process.Start(info) ?? throw new InvalidOperationException("No se pudo consultar FFmpeg.");
        using var registration = token.Register(() => { try { if (!process.HasExited) process.Kill(true); } catch (InvalidOperationException) { } });
        var stdout = process.StandardOutput.ReadToEndAsync(token); var stderr = process.StandardError.ReadToEndAsync(token);
        await process.WaitForExitAsync(token);
        var output = await stdout; var error = await stderr;
        if (process.ExitCode != 0) throw new InvalidOperationException(error);
        return output + error;
    }
}
