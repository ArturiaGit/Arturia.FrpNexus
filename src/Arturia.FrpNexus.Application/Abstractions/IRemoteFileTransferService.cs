namespace Arturia.FrpNexus.Application.Abstractions;

public interface IRemoteFileTransferService
{
    Task UploadFrpBinaryAsync(string nodeName, string localPath, string remotePath, CancellationToken cancellationToken = default);

    Task UploadConfigurationAsync(string nodeName, string tomlContent, string remotePath, CancellationToken cancellationToken = default);
}
