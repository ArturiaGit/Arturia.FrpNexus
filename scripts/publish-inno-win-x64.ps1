param(
    [string]$Version = "0.4.0-preview.3",
    [string]$RuntimeIdentifier = "win-x64",
    [string]$IconPath = "",
    [string]$InnoCompiler = ""
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$packageName = "FrpNexus-$RuntimeIdentifier-$Version"
$outputRoot = Join-Path $repoRoot "artifacts/release"
$outputPath = Join-Path $outputRoot $packageName
$sha256Path = Join-Path $outputRoot "$packageName.sha256.txt"
$defaultIconPath = Join-Path $repoRoot "src/Arturia.FrpNexus.Desktop/Assets/frpnexus-logo.ico"
$issPath = Join-Path $repoRoot "installer/frpnexus-preview.iss"
$velopackSetupPath = Join-Path $outputPath "Arturia.FrpNexus-win-Setup.exe"
$outputBaseName = "FrpNexus-Setup-$Version"

if ([string]::IsNullOrWhiteSpace($IconPath)) {
    $IconPath = $defaultIconPath
}

if (-not (Test-Path -LiteralPath $IconPath)) {
    throw "Icon file not found: $IconPath"
}

if (-not (Test-Path -LiteralPath $velopackSetupPath)) {
    throw "Velopack setup executable not found: $velopackSetupPath. Run the Velopack publish script first."
}

if (-not (Test-Path -LiteralPath $issPath)) {
    throw "Inno Setup script not found: $issPath"
}

if ([string]::IsNullOrWhiteSpace($InnoCompiler)) {
    $isccPath = $null
    $isccCandidates = @(
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
        "$env:PROGRAMFILES\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:PROGRAMFILES\Inno Setup 5\ISCC.exe",
        "${env:ProgramFiles(x86)}\Inno Setup 5\ISCC.exe"
    )
    foreach ($candidate in $isccCandidates) {
        if (Test-Path -LiteralPath $candidate) {
            $isccPath = $candidate
            break
        }
    }
}
else {
    $isccPath = $InnoCompiler
}

if ($null -eq $isccPath -or -not (Test-Path -LiteralPath $isccPath)) {
    throw @"
Inno Setup Compiler (ISCC.exe) not found. Install Inno Setup 6:

  winget install JRSoftware.InnoSetup

Or specify the compiler path:

  powershell -ExecutionPolicy Bypass -File scripts/publish-inno-win-x64.ps1 -InnoCompiler "C:\path\to\ISCC.exe"

"@
}

$existingOutput = Join-Path $outputPath "$outputBaseName.exe"
if (Test-Path -LiteralPath $existingOutput) {
    Remove-Item -LiteralPath $existingOutput -Force
}

Write-Host "Compiling Inno Setup installer..."
Write-Host "  Script  : $issPath"
Write-Host "  Version : $Version"
Write-Host "  Output  : $outputPath\$outputBaseName.exe"

$isccArguments = @(
    "/Qp",
    "/O`"$outputPath`"",
    "/F`"$outputBaseName`"",
    "/DAppVersion=`"$Version`"",
    "/DVelopackSetup=`"$velopackSetupPath`"",
    "/DSetupIcon=`"$IconPath`"",
    "/DOutputBase=`"$outputBaseName`"",
    "`"$issPath`""
)

& $isccPath @isccArguments
if ($LASTEXITCODE -ne 0) {
    throw "Inno Setup compilation failed with exit code $LASTEXITCODE."
}

$outerSetupPath = Join-Path $outputPath "$outputBaseName.exe"
if (-not (Test-Path -LiteralPath $outerSetupPath)) {
    throw "Outer Setup installer was not created: $outerSetupPath"
}

if (Test-Path -LiteralPath $sha256Path) {
    $relativePath = "$packageName\$outputBaseName.exe"
    $hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $outerSetupPath).Hash.ToLowerInvariant()
    $sha256Line = "$hash  $relativePath"
    $existingLines = Get-Content -LiteralPath $sha256Path |
        Where-Object { $_ -notmatch [regex]::Escape($relativePath) }
    $existingLines | Set-Content -LiteralPath $sha256Path -Encoding UTF8
    Add-Content -LiteralPath $sha256Path -Value $sha256Line -Encoding UTF8
    Write-Host "Updated SHA256 entry for outer Setup installer."
}

Write-Host "FrpNexus outer Setup installer created:"
Write-Host $outerSetupPath
Write-Host ""
Write-Host "Recommended download for users:"
Write-Host "  $outputBaseName.exe"
