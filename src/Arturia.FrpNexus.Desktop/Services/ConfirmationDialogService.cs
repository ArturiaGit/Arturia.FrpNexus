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
        var result = await ShowCoreAsync(
            request.Title,
            request.Message,
            request.ConfirmButtonText,
            request.CancelButtonText,
            request.Severity,
            secondaryButtonText: null,
            cancellationToken);

        return result == ConfirmationDialogResult.Confirm;
    }

    public Task<ConfirmationDialogResult> ShowChoiceAsync(
        ConfirmationDialogChoiceRequest request,
        CancellationToken cancellationToken = default)
    {
        return ShowCoreAsync(
            request.Title,
            request.Message,
            request.ConfirmButtonText,
            request.CancelButtonText,
            request.Severity,
            request.SecondaryButtonText,
            cancellationToken);
    }

    private async Task<ConfirmationDialogResult> ShowCoreAsync(
        string title,
        string message,
        string confirmButtonText,
        string cancelButtonText,
        string severity,
        string? secondaryButtonText,
        CancellationToken cancellationToken)
    {
        var taskCompletionSource = new TaskCompletionSource<ConfirmationDialogResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        ConfirmationDialogViewModel? viewModel = null;
        viewModel = new ConfirmationDialogViewModel(
            title,
            message,
            confirmButtonText,
            cancelButtonText,
            severity,
            result =>
            {
                taskCompletionSource.TrySetResult(result);
                if (viewModel is not null)
                {
                    modalDialogHostService.CloseDialog(viewModel);
                }
            },
            secondaryButtonText);

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
