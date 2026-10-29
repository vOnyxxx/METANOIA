[CmdletBinding()]
param([ValidatePattern('^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$')][string]$Version = '0.6.0')
$ErrorActionPreference = 'Stop'
$repository = Split-Path -Parent $PSScriptRoot
Push-Location $repository
function Invoke-DotNet {
    param([string[]]$Arguments)
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) { throw "dotnet failed: $($Arguments[0])" }
}
try {
    foreach ($tool in @('ffmpeg', 'ffprobe')) { if (-not (Get-Command $tool -ErrorAction SilentlyContinue)) { throw "$tool is required for integration tests" } }
    Invoke-DotNet @('restore', 'Metanoia.sln', '-r', 'win-x64')
    Invoke-DotNet @('build', 'Metanoia.sln', '-c', 'Release', '--no-restore', '-m:1')
    foreach ($suite in @('Metanoia.Core.Tests', 'Metanoia.IntegrationTests')) {
        Invoke-DotNet @('test', "tests/$suite/$suite.csproj", '-c', 'Release', '--no-build', '--no-restore', '--logger', "trx;LogFileName=$suite.trx", '--results-directory', 'artifacts/ci/tests')
    }
    Invoke-DotNet @('publish', 'src/Metanoia.App/Metanoia.App.csproj', '-c', 'Release', '-r', 'win-x64', '--self-contained', 'true', '--no-restore', '-m:1', '-p:PublishSingleFile=true', '-p:IncludeNativeLibrariesForSelfExtract=true', '-p:DebugType=embedded', '-p:Product=METANOIA', '-p:Title=METANOIA', "-p:Version=$Version", '-o', 'artifacts/ci/unsigned')
    foreach ($notice in @('LICENSE', 'PRIVACY.md', 'THIRD_PARTY_NOTICES.md')) { Copy-Item -LiteralPath $notice -Destination artifacts/ci/unsigned }
    $packageRoot = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { Join-Path $env:USERPROFILE '.nuget/packages' }
    $toolkit = Join-Path $packageRoot 'communitytoolkit.mvvm/8.4.2'
    Copy-Item -LiteralPath (Join-Path $toolkit 'License.md') -Destination artifacts/ci/unsigned/CommunityToolkit-LICENSE.md
    Copy-Item -LiteralPath (Join-Path $toolkit 'ThirdPartyNotices.txt') -Destination artifacts/ci/unsigned/CommunityToolkit-ThirdPartyNotices.txt
}
finally { Pop-Location }
