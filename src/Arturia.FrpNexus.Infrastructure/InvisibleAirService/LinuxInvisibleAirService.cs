using System.Runtime.InteropServices;
using Arturia.FrpNexus.Core.InvisibleAirService;

namespace Arturia.FrpNexus.Infrastructure.InvisibleAirService;

public sealed class LinuxInvisibleAirService(SystemdServiceUnitBuilder unitBuilder) : IInvisibleAirService
{
    public Task<InvisibleAirStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var platform = GetPlatform();
        var message = platform == ServicePlatform.Linux
            ? "Phase 7A 支持 Linux user-level systemd unit preview；未调用 systemctl，未安装或启动服务。"
            : $"当前平台为 {platform}；Phase 7A 只提供 Linux user-level systemd unit preview，不实现当前平台服务。";

        var status = new InvisibleAirStatus(
            BackgroundServiceStatus.Unavailable,
            ServiceMode.Foreground,
            TrayVisibility.Unavailable,
            message);

        return Task.FromResult(status);
    }

    public SystemdServiceUnitPreview PreviewUserServiceUnit(SystemdServiceUnitRequest request)
    {
        return unitBuilder.BuildUserServiceUnit(request);
    }

    public Task RequestBackgroundAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task RequestForegroundAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    private static ServicePlatform GetPlatform()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return ServicePlatform.Linux;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return ServicePlatform.Windows;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return ServicePlatform.MacOS;
        }

        return ServicePlatform.Unknown;
    }
}
