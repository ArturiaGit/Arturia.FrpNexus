using System.Threading;
using System.Threading.Tasks;

namespace Arturia.FrpNexus.Desktop.Services;

public interface IFrpCoreDownloadOptionsDialogService
{
    Task<FrpCoreDownloadOptions?> ShowAsync(CancellationToken cancellationToken = default);
}

public sealed record FrpCoreDownloadOptions(string BinaryName, string TargetRuntime);
