# Publishes self-contained single-file builds.
# Usage:  .\publish.ps1                      # all 4 platforms
#         .\publish.ps1 -Runtimes win-x64    # one platform
param(
    [string[]]$Runtimes = @("win-x64", "linux-x64", "osx-x64", "osx-arm64")
)

$publishArgs = @(
    "-c", "Release",
    "--self-contained", "true",
    "-p:PublishSingleFile=true",
    "-p:EnableCompressionInSingleFile=true",
    "-p:IncludeNativeLibrariesForSelfExtract=true",
    "-p:DebugType=embedded"
)

foreach ($r in $Runtimes) {
    Write-Host "=== Publishing $r ===" -ForegroundColor Cyan
    dotnet publish PZConfigurator -r $r @publishArgs
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    # native runtime packs leak .pdb files we don't want in the artifact
    Remove-Item "PZConfigurator/bin/Release/net10.0/$r/publish/*.pdb" -ErrorAction SilentlyContinue
    Write-Host "Output: PZConfigurator/bin/Release/net10.0/$r/publish/" -ForegroundColor Green
}
