using Arturia.FrpNexus.Application.Abstractions;
using Arturia.FrpNexus.Core.Models;
using Renci.SshNet;

namespace Arturia.FrpNexus.Infrastructure.Ssh;

internal static class SshConnectionInfoFactory
{
    public static ConnectionInfo Create(NodeProfile node, SshCredentialReference credential, string operationName)
    {
        var connectionInfo = credential.AuthenticationMode switch
        {
            SshAuthenticationMode.SessionPassword => CreatePasswordConnectionInfo(node, credential),
            SshAuthenticationMode.PrivateKey => CreatePrivateKeyConnectionInfo(node, credential),
            SshAuthenticationMode.SshAgent => throw new NotSupportedException($"SSH Agent 认证暂未接入 {operationName}，请先使用会话密码或私钥文件路径。"),
            _ => throw new NotSupportedException($"当前认证方式暂不支持 {operationName}。")
        };

        connectionInfo.Timeout = SshNetOperationPolicy.ConnectTimeout;
        return connectionInfo;
    }

    private static ConnectionInfo CreatePasswordConnectionInfo(NodeProfile node, SshCredentialReference credential)
    {
        if (string.IsNullOrWhiteSpace(credential.SessionPassword))
        {
            throw new InvalidOperationException("请输入本次会话使用的 SSH 密码。");
        }

        var passwordMethod = new PasswordAuthenticationMethod(
            node.UserName,
            credential.SessionPassword);
        var keyboardInteractiveMethod = new KeyboardInteractiveAuthenticationMethod(node.UserName);
        keyboardInteractiveMethod.AuthenticationPrompt += (_, args) =>
        {
            foreach (var prompt in args.Prompts)
            {
                prompt.Response = credential.SessionPassword;
            }
        };

        return new ConnectionInfo(
            node.Host,
            node.SshPort,
            node.UserName,
            passwordMethod,
            keyboardInteractiveMethod);
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
