using System.Diagnostics;

namespace Arturia.FrpNexus.Desktop.Services;

public sealed class LocalFolderLauncherService : ILocalFolderLauncherService
{
    public Task OpenFolderAsync(
        string folderPath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(folderPath))
        {
            throw new InvalidOperationException("目录路径不能为空。");
        }

        Directory.CreateDirectory(folderPath);
        Process.Start(new ProcessStartInfo
        {
            FileName = folderPath,
            UseShellExecute = true
        });

        return Task.CompletedTask;
    }
}
