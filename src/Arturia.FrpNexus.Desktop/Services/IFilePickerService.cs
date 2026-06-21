using System.Threading;
using System.Threading.Tasks;

namespace Arturia.FrpNexus.Desktop.Services;

public interface IFilePickerService
{
    Task<string?> PickFrpBinaryAsync(CancellationToken cancellationToken = default);

    Task<string?> PickLocalFrpcBinaryAsync(CancellationToken cancellationToken = default);

    Task<string?> PickLocalFrpcConfigPathAsync(
        string suggestedFileName,
        CancellationToken cancellationToken = default);

    Task<string?> PickFrpDownloadDirectoryAsync(CancellationToken cancellationToken = default);
}
