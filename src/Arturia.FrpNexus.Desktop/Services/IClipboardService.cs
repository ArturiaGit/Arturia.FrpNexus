using System.Threading;
using System.Threading.Tasks;

namespace Arturia.FrpNexus.Desktop.Services;

public interface IClipboardService
{
    Task SetTextAsync(string text, CancellationToken cancellationToken = default);
}
