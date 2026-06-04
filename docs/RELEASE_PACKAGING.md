# Release Packaging

This document describes the current FrpNexus preview packaging process.

## Current Package Type

Phase 4 starts with a Windows x64 folder-based preview package.

- Target runtime: `win-x64`.
- Configuration: `Release`.
- Deployment mode: self-contained.
- Version: `0.4.0-preview.1`.
- Output directory: `artifacts/release/FrpNexus-win-x64-0.4.0-preview.1/`.

The first Phase 4 package is intentionally not an installer, MSIX package, automatic updater, or single-file executable.

## Publish Command

Run from the repository root:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/publish-win-x64.ps1
```

The script removes the previous package directory for the same version and writes a fresh release folder under `artifacts/release/`.

## Manual Upgrade

For preview builds, manual upgrade means:

1. Close FrpNexus.
2. Replace the previous application folder with the new release folder.
3. Start `Arturia.FrpNexus.Desktop.exe` from the new folder.

User-local data is stored outside the release package under the user profile, so replacing the application folder must not delete local settings or records.

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

- Run `dotnet build`.
- Run full `dotnet test`.
- Run `powershell -ExecutionPolicy Bypass -File scripts/publish-win-x64.ps1`.
- Confirm `artifacts/release/FrpNexus-win-x64-0.4.0-preview.1/Arturia.FrpNexus.Desktop.exe` exists.
- Inspect the package folder and confirm it does not contain user data, logs, credentials, private keys, FRP caches, or test binaries.
