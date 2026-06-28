using System.Threading;
using System.Threading.Tasks;

namespace Arturia.FrpNexus.Desktop.Services;

public interface IOnboardingDialogService
{
    Task ShowIfRequiredAsync(CancellationToken cancellationToken = default);
}
