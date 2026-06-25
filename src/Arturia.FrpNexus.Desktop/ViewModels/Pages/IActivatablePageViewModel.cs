using System.Threading;
using System.Threading.Tasks;

namespace Arturia.FrpNexus.Desktop.ViewModels.Pages;

public interface IActivatablePageViewModel
{
    Task OnActivatedAsync(CancellationToken cancellationToken = default);

    void OnDeactivated();
}
