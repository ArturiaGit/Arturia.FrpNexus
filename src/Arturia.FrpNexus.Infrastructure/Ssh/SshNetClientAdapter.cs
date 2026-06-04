using Arturia.FrpNexus.Application.Abstractions;
using Arturia.FrpNexus.Core.Models;
using Renci.SshNet;

namespace Arturia.FrpNexus.Infrastructure.Ssh;

public sealed class SshNetClientAdapter : ISshClientAdapter
{
    public Task ConnectAsync(
        NodeProfile node,
        SshCredentialReference credential,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var client = new SshClient(SshConnectionInfoFactory.Create(node, credential, "SSH 连接测试"));
            client.Connect();

            cancellationToken.ThrowIfCancellationRequested();

            if (!client.IsConnected)
            {
                throw new InvalidOperationException("SSH 连接未成功建立。");
            }

            client.Disconnect();
        }, cancellationToken);
    }

}
