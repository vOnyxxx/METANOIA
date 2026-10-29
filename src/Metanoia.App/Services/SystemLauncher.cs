using System.Diagnostics;
using System.IO;

namespace Metanoia.App.Services;

public sealed class SystemLauncher : ISystemLauncher
{
    public void OpenContainingFolder(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        var directory = Path.GetDirectoryName(Path.GetFullPath(filePath));
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException("No se encontró la carpeta de salida.");
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = directory,
            UseShellExecute = true
        });
    }
}
