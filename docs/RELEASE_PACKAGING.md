# Release Packaging

This document describes the FrpNexus Windows packaging foundation for the `0.4.0-preview.2` release branch.

## Current Release Decision

Preview publishing is re-enabled by user decision for `release/v0.4.0-preview.2`.

- Use Velopack for the first-stage Windows x64 installer and update package.
- Use GitHub Releases as the public artifact and update metadata source.
- Publish a zip bundle and SHA256 checksum manifest alongside Velopack files.
- Defer MSIX until a trusted code-signing strategy is available.

## Current Package Type

The first packaging target is a Windows x64 Velopack preview package.

- Target runtime: `win-x64`.
- Configuration: `Release`.
- Deployment mode: self-contained.
- Version: `0.4.0-preview.2`.
- Output directory: `artifacts/release/FrpNexus-win-x64-0.4.0-preview.2/`.
- GitHub repository: `https://github.com/ArturiaGit/Arturia.FrpNexus`.
- Installer icon: `src/Arturia.FrpNexus.Desktop/Assets/frpnexus-logo.ico`.
- Installer location mode: `Either`, so users can choose the Velopack-supported install location mode.

The first package intentionally does not include MSIX or forced silent background update installation.

## Publish Command

Run from the repository root:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/publish-velopack-win-x64.ps1 -Version 0.4.0-preview.2
```

The script removes the previous package directory for the same version, publishes the desktop app, installs the matching local Velopack CLI if needed, writes a fresh Velopack release folder under `artifacts/release/`, creates a zip bundle, and writes a SHA256 manifest.

Optional packaging parameters:

- `-IconPath <path>` overrides the installer/package icon.
- `-InstallLocation <value>` forwards a Velopack install location value; the default is `Either`.

## Code Signing

The first-stage script allows unsigned packages because no trusted code-signing certificate is currently available.

- Unsigned Windows preview packages can trigger SmartScreen and antivirus warnings.
- Self-signed certificates are acceptable only for internal validation.
- Trusted signing should be added later through `-SignTemplate`, `-SignParams`, or `-AzureTrustedSignFile`.
- MSIX remains deferred because production MSIX distribution requires a trusted signing path.

## GitHub Releases Flow

1. Create tag `v0.4.0-preview.2` from the release branch after verification.
2. Create a GitHub Release draft for that tag.
3. Upload the Velopack release folder contents.
4. Upload `FrpNexus-win-x64-0.4.0-preview.2.zip`.
5. Upload `FrpNexus-win-x64-0.4.0-preview.2.sha256.txt`.
6. State in release notes whether the package is unsigned and that Windows may show a security warning.

Velopack clients use GitHub Releases as the release feed. The application currently initializes Velopack at startup but does not force a silent automatic update flow.

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
- Run `powershell -ExecutionPolicy Bypass -File scripts/publish-velopack-win-x64.ps1 -Version 0.4.0-preview.2`.
- Confirm the Velopack installer executable exists under `artifacts/release/FrpNexus-win-x64-0.4.0-preview.2/`.
- Confirm the installer uses the FrpNexus logo icon.
- Confirm the installer exposes the expected Velopack install location behavior.
- Confirm `artifacts/release/FrpNexus-win-x64-0.4.0-preview.2.zip` exists.
- Confirm `artifacts/release/FrpNexus-win-x64-0.4.0-preview.2.sha256.txt` exists.
- Inspect the package folder and confirm it does not contain user data, logs, credentials, private keys, FRP caches, or test binaries.
