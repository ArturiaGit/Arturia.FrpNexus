namespace Arturia.FrpNexus.Core.Models;

public sealed record NodeProfile(
    string Name,
    string Host,
    int SshPort,
    string UserName,
    string Authentication,
    string OperatingSystem,
    FrpNexusStatus ConnectionStatus,
    FrpNexusStatus FrpStatus,
    string FrpVersion,
    string Uptime,
    string ConfigPath,
    DateTimeOffset? LastConnectionTestedAt = null);
