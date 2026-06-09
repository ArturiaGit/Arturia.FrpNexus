using Arturia.FrpNexus.Core.InvisibleAirService;

namespace Arturia.FrpNexus.Infrastructure.Mocks;

public sealed class MockInvisibleAirService : IInvisibleAirService
{
    private ServiceMode _serviceMode = ServiceMode.Foreground;
    private TrayVisibility _trayVisibility = TrayVisibility.Unavailable;

    public Task<InvisibleAirStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var status = new InvisibleAirStatus(
            BackgroundServiceStatus.Unavailable,
            _serviceMode,
            _trayVisibility,
            "Mock service only. No system service or tray integration is installed.");

        return Task.FromResult(status);
    }

    public SystemdServiceUnitPreview PreviewUserServiceUnit(SystemdServiceUnitRequest request)
    {
        return new SystemdServiceUnitPreview(
            false,
            "frpnexus@mock.service",
            string.Empty,
            new[] { "MockInvisibleAirService 不生成 systemd unit preview。" },
            new[] { "Mock service only. No system service or tray integration is installed." });
    }

    public Task RequestBackgroundAsync(CancellationToken cancellationToken = default)
    {
        _serviceMode = ServiceMode.Background;
        _trayVisibility = TrayVisibility.Unavailable;

        return Task.CompletedTask;
    }

    public Task RequestForegroundAsync(CancellationToken cancellationToken = default)
    {
        _serviceMode = ServiceMode.Foreground;
        _trayVisibility = TrayVisibility.Unavailable;

        return Task.CompletedTask;
    }
}
