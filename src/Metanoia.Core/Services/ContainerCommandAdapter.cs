using Metanoia.Core.Models;
using System.Text.RegularExpressions;

namespace Metanoia.Core.Services;

public static class ContainerCommandAdapter
{
    /// <summary>Preserves transforms, trim and progress while letting a discovered muxer select codecs.</summary>
    public static FFmpegCommand Adapt(FFmpegCommand source, string output, string muxer, bool noVideoOrAudioOnly, bool stillImage)
    {
        if (!Regex.IsMatch(muxer, "^[a-zA-Z0-9_]+$")) throw new ArgumentException("Contenedor inválido.");
        var removed = new HashSet<string> { "-c:v", "-c:a", "-preset", "-crf", "-q:v", "-quality", "-b:a", "-movflags" };
        var args = new List<string>();
        for (var i = 0; i < source.Arguments.Count - 1; i++)
        {
            var arg = source.Arguments[i];
            if (removed.Contains(arg)) { i++; continue; }
            if (noVideoOrAudioOnly && arg == "-map" && source.Arguments[i + 1] == "0:a?") { i++; continue; }
            args.Add(arg);
        }
        // loudnorm can emit frames larger than FLAC's legal block size in FFmpeg 9.
        // Reblock samples without padding or altering the duration / measured loudness.
        if (muxer == "flac")
        {
            var filterIndex = args.IndexOf("-af");
            if (filterIndex >= 0) args[filterIndex + 1] += ",asetnsamples=n=4608:p=0";
            else args.AddRange(["-af", "asetnsamples=n=4608:p=0"]);
        }
        if (stillImage && muxer == "image2") args.AddRange(["-update", "1"]);
        args.AddRange(["-f", muxer, output]);
        FFmpegArgumentValidator.ValidateArguments(args);
        return new FFmpegCommand(source.Executable, args);
    }
}
