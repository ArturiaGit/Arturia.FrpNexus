namespace Arturia.FrpNexus.Desktop.Services;

public interface ILocalFolderLauncherService
{
    Task OpenFolderAsync(
        string folderPath,
        CancellationToken cancellationToken = default);
}
