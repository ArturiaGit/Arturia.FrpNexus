namespace Arturia.FrpNexus.Application.Abstractions;

public sealed record FrpNexusSettingsSnapshot(
    string FrpDownloadSource,
    string CoreDirectory,
    string ConfigDirectory,
    string LogDirectory,
    string SqliteDatabasePath);
