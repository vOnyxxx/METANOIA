namespace Metanoia.Infrastructure.FFmpeg;

public sealed class MediaProbeException : Exception
{
    public MediaProbeException(string message, string diagnostic)
        : base(message)
    {
        Diagnostic = diagnostic;
    }

    public string Diagnostic { get; }
}
