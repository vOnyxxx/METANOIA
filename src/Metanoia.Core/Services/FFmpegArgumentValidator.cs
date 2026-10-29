namespace Metanoia.Core.Services;

public static class FFmpegArgumentValidator
{
    private static readonly string[] BlockedTokens = ["&&", "||", ">", "<", ";"];

    public static void ValidateExecutable(string executable)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executable);

        var fileName = Path.GetFileName(executable);
        if (!string.Equals(fileName, "ffmpeg.exe", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(fileName, "ffmpeg", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Solo se permite el ejecutable FFmpeg.", nameof(executable));
        }
    }

    public static void ValidateArguments(IEnumerable<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        foreach (var argument in arguments)
        {
            if (argument is null)
            {
                throw new ArgumentException("La lista contiene un argumento nulo.", nameof(arguments));
            }

            if (argument.Contains('\0') || argument.Contains('\r') || argument.Contains('\n'))
            {
                throw new ArgumentException("Los argumentos no pueden contener caracteres de control.", nameof(arguments));
            }

            if (argument == "|" ||
                (argument.Contains('|') && !string.Equals(argument, "pipe:1", StringComparison.Ordinal)))
            {
                throw new ArgumentException("No se permiten operadores de tubería.", nameof(arguments));
            }

            if (BlockedTokens.Any(argument.Contains))
            {
                throw new ArgumentException("No se permiten operadores de shell.", nameof(arguments));
            }
        }
    }
}
