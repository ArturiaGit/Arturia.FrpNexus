using Arturia.FrpNexus.Application.Abstractions;
using Arturia.FrpNexus.Core.Models;
using Arturia.FrpNexus.Infrastructure.Ssh;
using Renci.SshNet;

namespace Arturia.FrpNexus.Infrastructure.Sftp;

public sealed class SshNetSftpClientAdapter : ISftpClientAdapter
{
    public Task UploadFileAsync(
        NodeProfile node,
        SshCredentialReference credential,
        Stream content,
        string remotePath,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var client = new SftpClient(SshConnectionInfoFactory.Create(node, credential, "SFTP"));
            client.Connect();

            cancellationToken.ThrowIfCancellationRequested();
            client.UploadFile(content, remotePath, canOverride: true);
            client.Disconnect();
        }, cancellationToken);
    }

}
