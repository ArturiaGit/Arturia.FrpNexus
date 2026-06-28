param(
    [string]$Version = "0.4.0-preview.1",
    [string]$Configuration = "Release",
    [string]$RuntimeIdentifier = "win-x64",
    [string]$VelopackVersion = "1.2.0",
    [string]$RepositoryUrl = "https://github.com/ArturiaGit/Arturia.FrpNexus",
    [string]$SignTemplate = "",
    [string]$SignParams = "",
    [string]$AzureTrustedSignFile = "",
    [switch]$SkipZip
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot "src/Arturia.FrpNexus.Desktop/Arturia.FrpNexus.Desktop.csproj"
$appId = "Arturia.FrpNexus"
$appTitle = "FrpNexus"
$packageName = "FrpNexus-$RuntimeIdentifier-$Version"
$publishPath = Join-Path $repoRoot ".artifacts/publish/$packageName"
$toolPath = Join-Path $repoRoot ".artifacts/tools"
$outputRoot = Join-Path $repoRoot "artifacts/release"
$outputPath = Join-Path $outputRoot $packageName
$zipPath = Join-Path $outputRoot "$packageName.zip"
$sha256Path = Join-Path $outputRoot "$packageName.sha256.txt"

function Get-Sha256Line {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [string]$BasePath
    )

    $baseUri = [System.Uri]::new((Join-Path (Resolve-Path -LiteralPath $BasePath).Path "."))
    $fileUri = [System.Uri]::new((Resolve-Path -LiteralPath $Path).Path)
    $relativePath = [System.Uri]::UnescapeDataString($baseUri.MakeRelativeUri($fileUri).ToString())
    $hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $Path).Hash.ToLowerInvariant()
    return "$hash  $relativePath"
}

function Invoke-Tool {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath,
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed with exit code $LASTEXITCODE`: $FilePath $($Arguments -join ' ')"
    }
}

if (Test-Path -LiteralPath $publishPath) {
    Remove-Item -LiteralPath $publishPath -Recurse -Force
}

if (Test-Path -LiteralPath $outputPath) {
    Remove-Item -LiteralPath $outputPath -Recurse -Force
}

if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

if (Test-Path -LiteralPath $sha256Path) {
    Remove-Item -LiteralPath $sha256Path -Force
}

New-Item -ItemType Directory -Path $publishPath -Force | Out-Null
New-Item -ItemType Directory -Path $outputPath -Force | Out-Null
New-Item -ItemType Directory -Path $toolPath -Force | Out-Null

dotnet publish $projectPath `
    --configuration $Configuration `
    --runtime $RuntimeIdentifier `
    --self-contained true `
    --output $publishPath `
    -p:Version=$Version `
    -p:AssemblyVersion=0.4.0.0 `
    -p:FileVersion=0.4.0.0 `
    -p:PublishSingleFile=false
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

dotnet tool update vpk --tool-path $toolPath --version $VelopackVersion | Out-Host
if ($LASTEXITCODE -ne 0) {
    dotnet tool install vpk --tool-path $toolPath --version $VelopackVersion | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to install Velopack CLI v$VelopackVersion."
    }
}

$vpkPath = Join-Path $toolPath "vpk.exe"
if (-not (Test-Path -LiteralPath $vpkPath)) {
    $vpkPath = Join-Path $toolPath "vpk"
}

if (-not (Test-Path -LiteralPath $vpkPath)) {
    throw "Velopack CLI was installed but the vpk executable was not found under $toolPath."
}

$packArguments = @(
    "pack",
    "--packId", $appId,
    "--packVersion", $Version,
    "--packTitle", $appTitle,
    "--packDir", $publishPath,
    "--mainExe", "Arturia.FrpNexus.Desktop.exe",
    "--outputDir", $outputPath,
    "--runtime", $RuntimeIdentifier
)

if (-not [string]::IsNullOrWhiteSpace($SignTemplate)) {
    $packArguments += @("--signTemplate", $SignTemplate)
}

if (-not [string]::IsNullOrWhiteSpace($SignParams)) {
    $packArguments += @("--signParams", $SignParams)
}

if (-not [string]::IsNullOrWhiteSpace($AzureTrustedSignFile)) {
    $packArguments += @("--azureTrustedSignFile", $AzureTrustedSignFile)
}

if ([string]::IsNullOrWhiteSpace($SignTemplate) -and
    [string]::IsNullOrWhiteSpace($SignParams) -and
    [string]::IsNullOrWhiteSpace($AzureTrustedSignFile)) {
    Write-Warning "No code-signing options were provided. The installer and update packages will be unsigned."
    Write-Warning "Unsigned Windows preview packages can trigger SmartScreen and antivirus warnings."
}

Invoke-Tool -FilePath $vpkPath -Arguments $packArguments

if (-not $SkipZip) {
    $releaseItems = Get-ChildItem -LiteralPath $outputPath
    Compress-Archive -LiteralPath $releaseItems.FullName -DestinationPath $zipPath -Force
}

$hashLines = New-Object System.Collections.Generic.List[string]
Get-ChildItem -LiteralPath $outputPath -File -Recurse |
    Sort-Object FullName |
    ForEach-Object { $hashLines.Add((Get-Sha256Line -Path $_.FullName -BasePath $outputPath)) }

if ((Test-Path -LiteralPath $zipPath) -and -not $SkipZip) {
    $hashLines.Add((Get-Sha256Line -Path $zipPath -BasePath $outputRoot))
}

$hashLines | Set-Content -LiteralPath $sha256Path -Encoding UTF8

$setupPath = Get-ChildItem -LiteralPath $outputPath -File |
    Where-Object { $_.Name -match 'Setup.*\.exe$' -or $_.Name -match 'Installer.*\.exe$' } |
    Select-Object -First 1
if ($null -eq $setupPath) {
    throw "Velopack package completed but no installer executable was found in $outputPath."
}

Write-Host "FrpNexus Velopack package created:"
Write-Host $outputPath
if (-not [string]::IsNullOrWhiteSpace($RepositoryUrl)) {
    Write-Host "GitHub Releases update source:"
    Write-Host $RepositoryUrl
}
if (-not $SkipZip) {
    Write-Host $zipPath
}
Write-Host $sha256Path
