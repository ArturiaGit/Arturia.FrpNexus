using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;

namespace Arturia.FrpNexus.Desktop.Services;

public sealed class AvaloniaFilePickerService : IFilePickerService
{
    public async Task<string?> PickFrpBinaryAsync(CancellationToken cancellationToken = default)
    {
        return await PickFrpBinaryCoreAsync("选择 frpc / frps 核心文件", cancellationToken);
    }

    public async Task<string?> PickLocalFrpcBinaryAsync(CancellationToken cancellationToken = default)
    {
        return await PickFrpBinaryCoreAsync("选择本地 frpc 核心文件", cancellationToken);
    }

    public async Task<string?> PickLocalFrpcConfigPathAsync(
        string suggestedFileName,
        CancellationToken cancellationToken = default)
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop
            || desktop.MainWindow is null)
        {
            return null;
        }

        var fileTypes = new[]
        {
            new FilePickerFileType("TOML 配置文件")
            {
                Patterns = ["*.toml"]
            },
            FilePickerFileTypes.All
        };

        var file = await desktop.MainWindow.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "选择本地 frpc.toml 路径",
            SuggestedFileName = string.IsNullOrWhiteSpace(suggestedFileName) ? "frpc.toml" : suggestedFileName,
            DefaultExtension = "toml",
            FileTypeChoices = fileTypes
        });

        cancellationToken.ThrowIfCancellationRequested();

        return file?.TryGetLocalPath();
    }

    public async Task<string?> PickFrpDownloadDirectoryAsync(CancellationToken cancellationToken = default)
    {
        return await PickLocalDirectoryAsync("选择 FRP 核心下载目录", cancellationToken);
    }

    public async Task<string?> PickLocalDirectoryAsync(
        string title,
        CancellationToken cancellationToken = default)
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop
            || desktop.MainWindow is null)
        {
            return null;
        }

        var folders = await desktop.MainWindow.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false
        });

        cancellationToken.ThrowIfCancellationRequested();

        return folders.Count == 0
            ? null
            : folders[0].TryGetLocalPath();
    }

    private static async Task<string?> PickFrpBinaryCoreAsync(
        string title,
        CancellationToken cancellationToken)
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop
            || desktop.MainWindow is null)
        {
            return null;
        }

        var fileTypes = new[]
        {
            new FilePickerFileType("FRP 核心文件")
            {
                Patterns = ["frpc*", "frps*", "*.exe"]
            },
            FilePickerFileTypes.All
        };

        var files = await desktop.MainWindow.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = fileTypes
        });

        cancellationToken.ThrowIfCancellationRequested();

        return files.Count == 0
            ? null
            : files[0].TryGetLocalPath();
    }
}
