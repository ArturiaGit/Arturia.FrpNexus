using System;
using System.Threading;
using System.Threading.Tasks;
using Arturia.FrpNexus.Desktop.ViewModels.Dialogs;

namespace Arturia.FrpNexus.Desktop.Services;

public sealed class ConfirmationDialogService(
    IModalOverlayService modalOverlayService,
    IModalDialogHostService modalDialogHostService) : IConfirmationDialogService
{
    public async Task<bool> ShowAsync(
        ConfirmationDialogRequest request,
        CancellationToken cancellationToken = default)
    {
        var taskCompletionSource = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        ConfirmationDialogViewModel? viewModel = null;
        viewModel = new ConfirmationDialogViewModel(
            request.Title,
            request.Message,
            request.ConfirmButtonText,
            request.CancelButtonText,
            request.Severity,
            result =>
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
