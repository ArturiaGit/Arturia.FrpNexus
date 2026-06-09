using System.Threading;
using System.Threading.Tasks;

namespace Arturia.FrpNexus.Desktop.Services;

public interface IFilePickerService
{
    Task<string?> PickFrpBinaryAsync(CancellationToken cancellationToken = default);
}
