using System.Windows;

namespace Metanoia.App.Services;

public sealed class ClipboardService : IClipboardService
{
    public void SetText(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        Clipboard.SetText(text);
    }
}
