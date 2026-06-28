# Quality Checks

This document lists the local verification commands that should also be suitable for CI.

## Standard Verification

Run these commands from the repository root:

```powershell
dotnet restore Arturia.FrpNexus.sln
dotnet build Arturia.FrpNexus.sln
dotnet test Arturia.FrpNexus.sln
dotnet format Arturia.FrpNexus.sln --verify-no-changes --verbosity minimal
```

## Locked Desktop Output Workaround

When the desktop app or a previous test host is still running, Windows may lock files under
`src/Arturia.FrpNexus.Desktop/bin/Debug/net8.0`. If standard build or test commands fail with a
`.NET Host` file lock, rerun the same verification with an isolated artifacts directory:

```powershell
$artifacts = Join-Path $env:TEMP ('frpnexus-build-' + [guid]::NewGuid().ToString('N'))
dotnet build Arturia.FrpNexus.sln --artifacts-path $artifacts

$artifacts = Join-Path $env:TEMP ('frpnexus-test-' + [guid]::NewGuid().ToString('N'))
dotnet test Arturia.FrpNexus.sln --artifacts-path $artifacts
```

## Current Format Status

`dotnet format Arturia.FrpNexus.sln --verify-no-changes --verbosity minimal` is intentionally
documented as a required check, but the current repository still has existing format debt. Known
categories include line ending mismatches and whitespace differences in older files.

Do not run `dotnet format` without `--verify-no-changes` as part of an unrelated change. Fix the
format debt in a dedicated formatting pass so code-quality changes do not get mixed with broad
line ending or whitespace churn.
