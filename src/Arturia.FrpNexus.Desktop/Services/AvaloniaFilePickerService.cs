using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;

namespace Arturia.FrpNexus.Desktop.Services;

public sealed class AvaloniaFilePickerService : IFilePickerService
{
    public async Task<string?> PickFrpBinaryAsync(CancellationToken cancellationToken = default)
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
            Title = "选择 frpc / frps 核心文件",
            AllowMultiple = false,
            FileTypeFilter = fileTypes
        });

        cancellationToken.ThrowIfCancellationRequested();

        return files.Count == 0
            ? null
            : files[0].TryGetLocalPath();
    }
}
