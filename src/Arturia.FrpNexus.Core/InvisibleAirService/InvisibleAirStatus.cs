namespace Arturia.FrpNexus.Core.InvisibleAirService;

public sealed record InvisibleAirStatus(
    BackgroundServiceStatus ServiceStatus,
    ServiceMode ServiceMode,
    TrayVisibility TrayVisibility,
    string StatusMessage);
