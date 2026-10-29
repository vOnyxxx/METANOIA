# METANOIA

A local Windows workspace for transforming images, video and audio with FFmpeg.

METANOIA provides focused workspaces, media inspection, previews, visual image cropping, color adjustments, video range selection, audio equalization and export. Generated processing commands remain visible. It is a pre-release project, not a full nonlinear editor or DAW.

## Requirements

- Windows x64.
- FFmpeg and FFprobe installed and available on PATH.
- For development: the .NET SDK specified by `global.json`.

## Build and test

```powershell
dotnet restore Metanoia.sln
dotnet build Metanoia.sln -c Release
dotnet test tests/Metanoia.Core.Tests/Metanoia.Core.Tests.csproj -c Release
dotnet test tests/Metanoia.IntegrationTests/Metanoia.IntegrationTests.csproj -c Release
```

Integration tests generate synthetic media locally. For the complete release build, run `pwsh -File scripts/Build-CI.ps1`.

## Downloads and Windows trust

The Windows workflow is manually dispatched from GitHub Actions and produces an explicitly **unsigned** build by default. Unsigned pre-release binaries may be blocked by Windows Smart App Control. Do not disable security protections to run them.

SignPath Foundation application preparation is in progress. No approval or production signature has been obtained. Only an artifact marked signed and passing Authenticode verification should be described as signed. A reproducible GitHub build is preparation for signing, not an immediate workaround for the block.

## Code signing policy

See [signing policy](docs/signing/SIGNING.md). Maintainer and release approver: [vOnyxxx](https://github.com/vOnyxxx). Foundation attribution will be added after actual approval; it is not claimed in advance.

## Privacy and licenses

Media processing is local. See [privacy](PRIVACY.md), [MIT license](LICENSE), and [third-party notices](THIRD_PARTY_NOTICES.md). FFmpeg/FFprobe are external dependencies and are not bundled in the application release.
