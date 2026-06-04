param(
    [string]$Version = "0.4.0-preview.1",
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot "src/Arturia.FrpNexus.Desktop/Arturia.FrpNexus.Desktop.csproj"
$packageName = "FrpNexus-win-x64-$Version"
$outputRoot = Join-Path $repoRoot "artifacts/release"
$outputPath = Join-Path $outputRoot $packageName

if (Test-Path -LiteralPath $outputPath) {
    Remove-Item -LiteralPath $outputPath -Recurse -Force
}

New-Item -ItemType Directory -Path $outputPath -Force | Out-Null

dotnet publish $projectPath `
    --configuration $Configuration `
    --runtime win-x64 `
    --self-contained true `
    --output $outputPath `
    -p:Version=$Version `
    -p:AssemblyVersion=0.4.0.0 `
    -p:FileVersion=0.4.0.0 `
    -p:PublishSingleFile=false

$exePath = Join-Path $outputPath "Arturia.FrpNexus.Desktop.exe"
if (-not (Test-Path -LiteralPath $exePath)) {
    throw "Publish completed but executable was not found: $exePath"
}

Write-Host "FrpNexus Windows x64 package created:"
Write-Host $outputPath
