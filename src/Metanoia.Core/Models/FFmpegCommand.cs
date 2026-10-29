using System.Text;

namespace Metanoia.Core.Models;

public sealed record FFmpegCommand
{
    public FFmpegCommand(string executable, IEnumerable<string> arguments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executable);
        ArgumentNullException.ThrowIfNull(arguments);

        Executable = executable;
        Arguments = arguments.ToArray();
    }

    public string Executable { get; }

    public IReadOnlyList<string> Arguments { get; }

    public string ToDisplayString()
    {
        var builder = new StringBuilder(QuoteForDisplay(Executable));

        foreach (var argument in Arguments)
        {
            builder.Append(' ');
            builder.Append(QuoteForDisplay(argument));
        }

        return builder.ToString();
    }

    public static string QuoteForDisplay(string value)
    {
        if (value.Length > 0 && value.All(character =>
                !char.IsWhiteSpace(character) && character is not '"'))
        {
            return value;
        }

        var builder = new StringBuilder("\"");
        var pendingBackslashes = 0;

        foreach (var character in value)
        {
            if (character == '\\')
            {
                pendingBackslashes++;
                continue;
            }

            if (character == '"')
            {
                builder.Append('\\', pendingBackslashes * 2 + 1);
                builder.Append('"');
                pendingBackslashes = 0;
                continue;
            }

            builder.Append('\\', pendingBackslashes);
            pendingBackslashes = 0;
            builder.Append(character);
        }

        builder.Append('\\', pendingBackslashes * 2);
        builder.Append('"');
        return builder.ToString();
    }
}
