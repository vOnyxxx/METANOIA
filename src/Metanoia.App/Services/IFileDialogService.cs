using Metanoia.Core.Models;

namespace Metanoia.App.Services;

public interface IFileDialogService
{
    string? SelectInputMedia();

    string? SelectOutputVideo(string suggestedPath, VideoOutputMode mode);

    string? SelectOutputImage(string suggestedPath, string format);

    string? SelectOutputAudio(string suggestedPath, AudioOutputFormat format);
}
