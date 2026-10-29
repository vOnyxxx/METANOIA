# Third-party software

METANOIA's original source code is MIT licensed; third-party components retain their licenses.

| Component | Use | License / source |
| --- | --- | --- |
| .NET Community Toolkit / CommunityToolkit.Mvvm | Runtime MVVM support | MIT; https://github.com/CommunityToolkit/dotnet |
| .NET / WPF runtime | Self-contained Windows runtime | MIT and component notices; https://github.com/dotnet/runtime and https://github.com/dotnet/wpf |
| xUnit | Development/tests only | Apache-2.0; https://github.com/xunit/xunit |
| Microsoft.NET.Test.Sdk and test adapters | Development/tests only | Their package licenses and notices |
| FFmpeg / FFprobe | External user-installed tools | LGPL/GPL depending on build; https://ffmpeg.org/legal.html |

The release does not bundle FFmpeg or FFprobe. Installing or redistributing them requires observing the license of the selected build. CI installs FFmpeg only in the build runner to run integration tests; it is not included in the uploaded application artifact.

The build script copies the Community Toolkit's complete MIT license and package notices into the release. Self-contained .NET runtime notices supplied by the SDK remain in the published directory. License documents are included along with the executable, not replaced by this summary.
