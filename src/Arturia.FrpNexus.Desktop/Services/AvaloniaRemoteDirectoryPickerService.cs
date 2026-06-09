using System.Threading;
using System.Threading.Tasks;
using Arturia.FrpNexus.Application.Abstractions;
using Arturia.FrpNexus.Core.Models;
using Arturia.FrpNexus.Desktop.ViewModels.Dialogs;
using Arturia.FrpNexus.Desktop.Views.Dialogs;
using Avalonia.Controls.ApplicationLifetimes;

namespace Arturia.FrpNexus.Desktop.Services;

public sealed class AvaloniaRemoteDirectoryPickerService(
    IRemoteDirectoryService remoteDirectoryService) : IRemoteDirectoryPickerService
{
    public async Task<string?> PickRemoteDirectoryAsync(
        NodeProfile node,
        SshCredentialReference credential,
        string initialDirectory,
        CancellationToken cancellationToken = default)
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop
            || desktop.MainWindow is null)
        {
            return null;
        }

        var taskCompletionSource = new TaskCompletionSource<string?>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var window = new RemoteDirectoryPickerWindow();
        var viewModel = new RemoteDirectoryPickerViewModel(
            remoteDirectoryService,
            node,
            credential,
            initialDirectory,
            result =>
            {
                taskCompletionSource.TrySetResult(result);
                window.Close();
            });

        window.DataContext = viewModel;
        window.Closed += (_, _) => taskCompletionSource.TrySetResult(null);

        await viewModel.LoadAsync(cancellationToken);
        window.Show(desktop.MainWindow);

        await using var registration = cancellationToken.Register(
            () =>
            {
                taskCompletionSource.TrySetCanceled(cancellationToken);
                window.Close();
            });

        return await taskCompletionSource.Task;
    }
}
