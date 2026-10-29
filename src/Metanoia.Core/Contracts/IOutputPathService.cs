using Metanoia.Core.Models;

namespace Metanoia.Core.Contracts;

public interface IOutputPathService
{
    string SuggestVideoOutputPath(string inputPath, VideoOutputMode mode = VideoOutputMode.Mp4);

    string SuggestImageOutputPath(string inputPath, ImageOutputFormat format);

    string SuggestAudioOutputPath(string inputPath, AudioOutputFormat format);

    void ValidateVideoOutputPath(string inputPath, string outputPath, VideoOutputMode mode = VideoOutputMode.Mp4);

    void ValidateImageOutputPath(string inputPath, string outputPath, ImageOutputFormat format);

    void ValidateAudioOutputPath(string inputPath, string outputPath, AudioOutputFormat format);
}
