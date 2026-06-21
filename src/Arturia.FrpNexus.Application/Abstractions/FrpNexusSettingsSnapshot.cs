namespace Arturia.FrpNexus.Application.Abstractions;

public sealed record FrpNexusSettingsSnapshot(
    string FrpDownloadSource,
    string LogDirectory,
    string SqliteDatabasePath,
    string CustomFrpDownloadSourceUrl = "");
