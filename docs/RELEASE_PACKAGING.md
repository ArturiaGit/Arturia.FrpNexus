# Release Packaging

This document describes the FrpNexus Windows packaging foundation for the `0.4.0-preview.3` release branch.

## Current Release Decision

Preview publishing is re-enabled by user decision for `release/v0.4.0-preview.3`.

- Use Velopack for the first-stage Windows x64 installer and update package.
- Use GitHub Releases as the public artifact and update metadata source.
- Publish a zip bundle and SHA256 checksum manifest alongside Velopack files.
- Defer MSIX until a trusted code-signing strategy is available.

## Current Package Type

The first packaging target is a Windows x64 Velopack preview package.

- Target runtime: `win-x64`.
- Configuration: `Release`.
- Deployment mode: self-contained.
- Version: `0.4.0-preview.3`.
- Output directory: `artifacts/release/FrpNexus-win-x64-0.4.0-preview.3/`.
- GitHub repository: `https://github.com/ArturiaGit/Arturia.FrpNexus`.
- Installer icon: `src/Arturia.FrpNexus.Desktop/Assets/frpnexus-logo.ico`.
- Installer location mode: `Either`, so users can choose the Velopack-supported install location mode.

The first package intentionally does not include MSIX or forced silent background update installation.

## Outer Setup Installer (Inno Setup)

The Velopack native installer (`Arturia.FrpNexus-win-Setup.exe`) does not show a directory selection page when double-clicked. For normal users who need a GUI installation directory picker, an outer Inno Setup installer wraps the Velopack installer:

- Inner Velopack installer: `Arturia.FrpNexus-win-Setup.exe` (used for auto-update feed).
- Outer Setup installer: `FrpNexus-Setup-0.4.0-preview.3.exe` (recommended download for end users).
- The outer installer presents a directory selection page, then invokes the inner Velopack installer with `--installto {app}`.
- The outer installer is a bootstrapper and does not register its own uninstaller. Velopack remains responsible for the real installed application and uninstall entry.
- If `%LocalAppData%\Arturia.FrpNexus` already exists, the outer installer stops and asks the user to uninstall the existing Velopack installation first. This avoids an empty custom directory that only contains Inno Setup uninstall files.
- Inno Setup script: `installer/frpnexus-preview.iss`
- Build script: `scripts/publish-inno-win-x64.ps1`

## Publish Commands

Run from the repository root in order:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/publish-velopack-win-x64.ps1 -Version 0.4.0-preview.3
powershell -ExecutionPolicy Bypass -File scripts/publish-inno-win-x64.ps1 -Version 0.4.0-preview.3
```

The Velopack script removes the previous package directory for the same version, publishes the desktop app, installs the matching local Velopack CLI if needed, writes a fresh Velopack release folder under `artifacts/release/`, creates a zip bundle, and writes a SHA256 manifest.

The Inno Setup script compiles `installer/frpnexus-preview.iss`, places `FrpNexus-Setup-0.4.0-preview.3.exe` into the same release directory, and appends its SHA256 entry to the manifest.

Optional packaging parameters:

- `-IconPath <path>` overrides the installer/package icon.
- `-InstallLocation <value>` forwards a Velopack install location value; the default is `Either`.
- `-InnoCompiler <path>` overrides the Inno Setup compiler path.

## Code Signing

The first-stage script allows unsigned packages because no trusted code-signing certificate is currently available.

- Unsigned Windows preview packages can trigger SmartScreen and antivirus warnings.
- Self-signed certificates are acceptable only for internal validation.
- Trusted signing should be added later through `-SignTemplate`, `-SignParams`, or `-AzureTrustedSignFile`.
- MSIX remains deferred because production MSIX distribution requires a trusted signing path.

## GitHub Releases Flow

1. Create tag `v0.4.0-preview.3` from the release branch after verification.
2. Create a GitHub Release draft for that tag.
3. Upload the outer Setup installer `FrpNexus-Setup-0.4.0-preview.3.exe` as the recommended download.
4. Upload the Velopack release folder contents (for auto-update feed).
5. Upload `FrpNexus-win-x64-0.4.0-preview.3.zip`.
6. Upload `FrpNexus-win-x64-0.4.0-preview.3.sha256.txt`.
7. State in release notes whether the package is unsigned and that Windows may show a security warning.

Velopack clients use GitHub Releases as the release feed. The application currently initializes Velopack at startup but does not force a silent automatic update flow.
End users should download `FrpNexus-Setup-0.4.0-preview.3.exe` for the GUI installation experience.

## Release Package Must Not Include

- User SQLite databases.
- User logs.
- SSH passwords.
- Tokens.
- Private key contents.
- Private key passphrases.
- User FRP cache directories.
- Test project outputs.

## Release Check List

Before sharing a preview package:

- Run `dotnet restore`.
- Run `dotnet build`.
- Run full `dotnet test`.
- Run `powershell -ExecutionPolicy Bypass -File scripts/publish-velopack-win-x64.ps1 -Version 0.4.0-preview.3`.
- Run `powershell -ExecutionPolicy Bypass -File scripts/publish-inno-win-x64.ps1 -Version 0.4.0-preview.3`.
- Confirm the Velopack installer executable exists under `artifacts/release/FrpNexus-win-x64-0.4.0-preview.3/`.
- Confirm the outer Setup installer `FrpNexus-Setup-0.4.0-preview.3.exe` exists in the same directory.
- Confirm the outer Setup installer shows a directory selection page with the FrpNexus logo icon.
- Confirm the outer Setup installer stops with a clear message when `%LocalAppData%\Arturia.FrpNexus` already exists.
- Confirm the outer Setup installer does not leave `unins000.exe` or `unins000.dat` in the selected custom directory.
- Confirm selecting a custom directory installs the application files to that directory.
- Confirm the application launches after installation.
- Confirm `artifacts/release/FrpNexus-win-x64-0.4.0-preview.3.zip` exists.
- Confirm `artifacts/release/FrpNexus-win-x64-0.4.0-preview.3.sha256.txt` exists and includes the outer Setup installer entry.
- Inspect the package folder and confirm it does not contain user data, logs, credentials, private keys, FRP caches, or test binaries.
