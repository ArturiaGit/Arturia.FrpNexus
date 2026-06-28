using System.Threading;
using System.Threading.Tasks;
using Arturia.FrpNexus.Desktop.ViewModels.Dialogs;

namespace Arturia.FrpNexus.Desktop.Services;

public sealed class FrpCoreDownloadOptionsDialogService(
    IModalOverlayService modalOverlayService,
    IModalDialogHostService modalDialogHostService) : IFrpCoreDownloadOptionsDialogService
{
    public async Task<FrpCoreDownloadOptions?> ShowAsync(CancellationToken cancellationToken = default)
    {
        var taskCompletionSource = new TaskCompletionSource<FrpCoreDownloadOptions?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        FrpCoreDownloadOptionsDialogViewModel? viewModel = null;
        viewModel = new FrpCoreDownloadOptionsDialogViewModel(result =>
        {
            taskCompletionSource.TrySetResult(result);
            if (viewModel is not null)
            {
                modalDialogHostService.CloseDialog(viewModel);
            }
        });

        using var overlayScope = modalOverlayService.ShowOverlay();

        try
        {
            modalDialogHostService.ShowDialog(viewModel);
            await using var registration = cancellationToken.Register(
                () =>
                {
                    taskCompletionSource.TrySetCanceled(cancellationToken);
                    modalDialogHostService.CloseDialog(viewModel);
                });

            return await taskCompletionSource.Task;
        }
        finally
        {
            modalDialogHostService.CloseDialog(viewModel);
            overlayScope.Dispose();
        }
    }
}
