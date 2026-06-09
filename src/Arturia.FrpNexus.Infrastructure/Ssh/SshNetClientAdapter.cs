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

    public Task<ISshClientSession> OpenSessionAsync(
        NodeProfile node,
        SshCredentialReference credential,
        CancellationToken cancellationToken = default)
    {
        return Task.Run<ISshClientSession>(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var client = new SshClient(SshConnectionInfoFactory.Create(node, credential, "SSH 节点连接"));
            try
            {
                client.Connect();

                cancellationToken.ThrowIfCancellationRequested();

                if (!client.IsConnected)
                {
                    throw new InvalidOperationException("SSH 连接未成功建立。");
                }

                return new SshNetClientSession(client);
            }
            catch
            {
                client.Dispose();
                throw;
            }
        }, cancellationToken);
    }

    private sealed class SshNetClientSession(SshClient client) : ISshClientSession
    {
        public bool IsConnected => client.IsConnected;

        public void Disconnect()
        {
            if (client.IsConnected)
            {
                client.Disconnect();
            }
        }

        public void Dispose()
        {
            Disconnect();
            client.Dispose();
        }
    }
}
