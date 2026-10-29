using System.Text;
using Metanoia.Core.Models;

namespace Metanoia.Core.Services;

public sealed class AdvancedArgumentParser
{
    private static readonly IReadOnlyDictionary<string, bool> AllowedOptions =
        new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase)
        {
            ["-metadata"] = true,
            ["-movflags"] = true,
            ["-pix_fmt"] = true,
            ["-threads"] = true,
            ["-map_metadata"] = true,
            ["-map_chapters"] = true,
            ["-color_range"] = true,
            ["-color_primaries"] = true,
            ["-color_trc"] = true,
            ["-colorspace"] = true,
            ["-disposition:a"] = true,
            ["-disposition:v"] = true,
            ["-shortest"] = false,
            ["-vn"] = false,
            ["-an"] = false,
            ["-sn"] = false,
            ["-dn"] = false
        };

    public IReadOnlyList<string> Parse(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return [];
        }

        var tokens = Tokenize(input);
        FFmpegArgumentValidator.ValidateArguments(tokens);

        var validated = new List<string>(tokens.Count);
        for (var index = 0; index < tokens.Count; index++)
        {
            var option = tokens[index];
            if (!AllowedOptions.TryGetValue(option, out var requiresValue))
            {
                throw new ArgumentException($"La opción avanzada '{option}' no está permitida.", nameof(input));
            }

            validated.Add(option);
            if (!requiresValue)
            {
                continue;
            }

            if (++index >= tokens.Count || tokens[index].StartsWith('-'))
            {
                throw new ArgumentException($"La opción '{option}' requiere un valor.", nameof(input));
            }

            validated.Add(tokens[index]);
        }

        return validated;
    }

    public FFmpegCommand Apply(FFmpegCommand command, string? input)
    {
        ArgumentNullException.ThrowIfNull(command);
        var additionalArguments = Parse(input);
        if (additionalArguments.Count == 0)
        {
            return command;
        }

        if (command.Arguments.Count == 0)
        {
            throw new ArgumentException("El comando base no contiene una salida protegida.", nameof(command));
        }

        var arguments = command.Arguments.Take(command.Arguments.Count - 1)
            .Concat(additionalArguments)
            .Append(command.Arguments[^1]);
        return new FFmpegCommand(command.Executable, arguments);
    }

    private static IReadOnlyList<string> Tokenize(string input)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (var index = 0; index < input.Length; index++)
        {
            var character = input[index];
            if (character is '\r' or '\n' or '\0')
            {
                throw new ArgumentException("Los argumentos avanzados deben escribirse en una sola línea.", nameof(input));
            }

            if (character == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (char.IsWhiteSpace(character) && !inQuotes)
            {
                AddCurrentToken(result, current);
                continue;
            }

            current.Append(character);
        }

        if (inQuotes)
        {
            throw new ArgumentException("Hay comillas sin cerrar en los argumentos avanzados.", nameof(input));
        }

        AddCurrentToken(result, current);
        return result;
    }

    private static void AddCurrentToken(ICollection<string> result, StringBuilder current)
    {
        if (current.Length == 0)
        {
            return;
        }

        result.Add(current.ToString());
        current.Clear();
    }
}
