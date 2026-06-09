using System;
using System.Threading;
using System.Threading.Tasks;
using Arturia.FrpNexus.Application.Abstractions;
using Arturia.FrpNexus.Core.Models;
using Arturia.FrpNexus.Desktop.ViewModels.Dialogs;

namespace Arturia.FrpNexus.Desktop.Services;

public sealed class AvaloniaNodeConnectionWorkflowDialogService(
    INodeConnectionSessionService nodeConnectionSessionService,
    IRemoteFileTransferService remoteFileTransferService,
    IRemoteRuntimeService remoteRuntimeService,
    ITomlConfigurationService tomlConfigurationService,
    IFilePickerService filePickerService,
    IRemoteDirectoryPickerService remoteDirectoryPickerService,
    INodeCredentialSecretService nodeCredentialSecretService,
    IDeploymentRecordService deploymentRecordService,
    IModalOverlayService modalOverlayService,
    IModalDialogHostService modalDialogHostService) : INodeConnectionWorkflowDialogService
{
    public async Task<NodeConnectionWorkflowResult> ShowAsync(
        NodeProfile node,
        NodeConnectionWorkflowOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var taskCompletionSource = new TaskCompletionSource<NodeConnectionWorkflowResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var loadCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        NodeConnectionWorkflowViewModel? viewModel = null;
        viewModel = new NodeConnectionWorkflowViewModel(
            nodeConnectionSessionService,
            remoteFileTransferService,
            remoteRuntimeService,
            tomlConfigurationService,
            filePickerService,
            remoteDirectoryPickerService,
            nodeCredentialSecretService,
            deploymentRecordService,
            node,
            options ?? NodeConnectionWorkflowOptions.Default,
            result =>
            {
                taskCompletionSource.TrySetResult(result);
                CancelLoad(loadCancellationTokenSource);
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
                    CancelLoad(loadCancellationTokenSource);
                    modalDialogHostService.CloseDialog(viewModel);
                });

            _ = LoadDialogAsync(viewModel, taskCompletionSource, loadCancellationTokenSource);

            return await taskCompletionSource.Task;
        }
        finally
        {
            modalDialogHostService.CloseDialog(viewModel);
            overlayScope.Dispose();
        }
    }

    private static async Task LoadDialogAsync(
        NodeConnectionWorkflowViewModel viewModel,
        TaskCompletionSource<NodeConnectionWorkflowResult> taskCompletionSource,
        CancellationTokenSource cancellationTokenSource)
    {
        try
        {
            var cancellationToken = cancellationTokenSource.Token;
            await viewModel.LoadAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationTokenSource.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            taskCompletionSource.TrySetException(ex);
        }
        finally
        {
            cancellationTokenSource.Dispose();
        }
    }

    private static void CancelLoad(CancellationTokenSource cancellationTokenSource)
    {
        try
        {
            cancellationTokenSource.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
    }
}
