using Arturia.FrpNexus.Application.Abstractions;
using Arturia.FrpNexus.Core.Models;
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

            if (credential.AuthenticationMode == SshAuthenticationMode.SshAgent)
            {
                throw new NotSupportedException("SSH Agent 认证暂未接入 SFTP，请先使用会话密码或私钥文件路径。");
            }

            using var client = new SftpClient(CreateConnectionInfo(node, credential));
            client.Connect();

            cancellationToken.ThrowIfCancellationRequested();
            client.UploadFile(content, remotePath, canOverride: true);
            client.Disconnect();
        }, cancellationToken);
    }

    private static ConnectionInfo CreateConnectionInfo(NodeProfile node, SshCredentialReference credential)
    {
        return credential.AuthenticationMode switch
        {
            SshAuthenticationMode.SessionPassword => CreatePasswordConnectionInfo(node, credential),
            SshAuthenticationMode.PrivateKey => CreatePrivateKeyConnectionInfo(node, credential),
            _ => throw new NotSupportedException("当前认证方式暂不支持 SFTP。")
        };
    }

    private static ConnectionInfo CreatePasswordConnectionInfo(NodeProfile node, SshCredentialReference credential)
    {
        if (string.IsNullOrWhiteSpace(credential.SessionPassword))
        {
            throw new InvalidOperationException("请输入本次会话使用的 SSH 密码。");
        }

        return new PasswordConnectionInfo(
            node.Host,
            node.SshPort,
            node.UserName,
            credential.SessionPassword);
    }

    private static ConnectionInfo CreatePrivateKeyConnectionInfo(NodeProfile node, SshCredentialReference credential)
    {
        if (string.IsNullOrWhiteSpace(credential.PrivateKeyPath))
        {
            throw new InvalidOperationException("请选择私钥文件路径。");
        }

        if (!File.Exists(credential.PrivateKeyPath))
        {
            throw new FileNotFoundException("私钥文件不存在。", credential.PrivateKeyPath);
        }

        var keyFile = string.IsNullOrWhiteSpace(credential.PrivateKeyPassphrase)
            ? new PrivateKeyFile(credential.PrivateKeyPath)
            : new PrivateKeyFile(credential.PrivateKeyPath, credential.PrivateKeyPassphrase);

        return new PrivateKeyConnectionInfo(
            node.Host,
            node.SshPort,
            node.UserName,
            keyFile);
    }
}
