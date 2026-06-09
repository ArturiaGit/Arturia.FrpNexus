namespace Arturia.FrpNexus.Core.InvisibleAirService;

public interface IInvisibleAirService
{
    Task<InvisibleAirStatus> GetStatusAsync(CancellationToken cancellationToken = default);

    SystemdServiceUnitPreview PreviewUserServiceUnit(SystemdServiceUnitRequest request);

    Task RequestBackgroundAsync(CancellationToken cancellationToken = default);

    Task RequestForegroundAsync(CancellationToken cancellationToken = default);
}
