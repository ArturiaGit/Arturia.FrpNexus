using Arturia.FrpNexus.Application.Abstractions;
using Arturia.FrpNexus.Core.Models;
using Arturia.FrpNexus.Infrastructure.Ssh;
using Renci.SshNet;
using Renci.SshNet.Common;
using System.Security.Cryptography;

namespace Arturia.FrpNexus.Tests.Infrastructure;

public sealed class SshConnectionInfoFactoryTests
{
    [Fact]
    public void Create_ShouldUsePasswordAndKeyboardInteractiveForSessionPassword()
    {
        var node = CreateNode();
        var credential = new SshCredentialReference(
            SshAuthenticationMode.SessionPassword,
            SessionPassword: "SESSION_PASSWORD_PLACEHOLDER");

        var connectionInfo = SshConnectionInfoFactory.Create(node, credential, "SSH 连接测试");

        Assert.Equal(node.Host, connectionInfo.Host);
        Assert.Equal(node.SshPort, connectionInfo.Port);
        Assert.Equal(node.UserName, connectionInfo.Username);
        Assert.Contains(connectionInfo.AuthenticationMethods, method => method is PasswordAuthenticationMethod);
        Assert.Contains(connectionInfo.AuthenticationMethods, method => method is KeyboardInteractiveAuthenticationMethod);
    }

    [Fact]
    public void Create_ShouldApplyDefaultConnectionTimeoutForSessionPassword()
    {
        var connectionInfo = SshConnectionInfoFactory.Create(
            CreateNode(),
            new SshCredentialReference(
                SshAuthenticationMode.SessionPassword,
                SessionPassword: "SESSION_PASSWORD_PLACEHOLDER"),
            "SSH 连接测试");

        Assert.Equal(SshNetOperationPolicy.ConnectTimeout, connectionInfo.Timeout);
    }

    [Fact]
    public void Create_ShouldApplyDefaultConnectionTimeoutForPrivateKey()
    {
        var privateKeyPath = CreateTemporaryPrivateKeyFile();
        var connectionInfo = SshConnectionInfoFactory.Create(
            CreateNode(),
            new SshCredentialReference(
                SshAuthenticationMode.PrivateKey,
                PrivateKeyPath: privateKeyPath),
            "SSH 连接测试");

        Assert.Equal(SshNetOperationPolicy.ConnectTimeout, connectionInfo.Timeout);
    }

    [Fact]
    public void Create_ShouldFillKeyboardInteractivePromptsWithSessionPassword()
    {
        var credential = new SshCredentialReference(
            SshAuthenticationMode.SessionPassword,
            SessionPassword: "SESSION_PASSWORD_PLACEHOLDER");
        var connectionInfo = SshConnectionInfoFactory.Create(CreateNode(), credential, "SSH 连接测试");
        var keyboardInteractive = Assert.IsType<KeyboardInteractiveAuthenticationMethod>(
            connectionInfo.AuthenticationMethods.Single(method => method is KeyboardInteractiveAuthenticationMethod));
        var prompts = new[]
        {
            new AuthenticationPrompt(0, false, "Password:")
        };
        var args = new AuthenticationPromptEventArgs(
            connectionInfo.Username,
            string.Empty,
            string.Empty,
            prompts);

        keyboardInteractive.RaiseAuthenticationPrompt(args);

        Assert.Equal("SESSION_PASSWORD_PLACEHOLDER", prompts[0].Response);
    }

    [Fact]
    public void Create_ShouldRejectEmptySessionPassword()
    {
        var credential = new SshCredentialReference(SshAuthenticationMode.SessionPassword);

        var exception = Assert.Throws<InvalidOperationException>(
            () => SshConnectionInfoFactory.Create(CreateNode(), credential, "SSH 连接测试"));

        Assert.Equal("请输入本次会话使用的 SSH 密码。", exception.Message);
    }

    [Fact]
    public void Create_ShouldNotExposeSessionPasswordThroughConnectionInfoText()
    {
        var credential = new SshCredentialReference(
            SshAuthenticationMode.SessionPassword,
            SessionPassword: "SESSION_PASSWORD_PLACEHOLDER");

        var connectionInfo = SshConnectionInfoFactory.Create(CreateNode(), credential, "SSH 连接测试");

        Assert.DoesNotContain("SESSION_PASSWORD_PLACEHOLDER", connectionInfo.ToString(), StringComparison.Ordinal);
    }

    private static NodeProfile CreateNode()
    {
        return new NodeProfile(
            "测试节点",
            "203.0.113.10",
            22,
            "deploy",
            "会话密码",
            "Ubuntu 22.04 LTS",
            FrpNexusStatus.Offline,
            FrpNexusStatus.Stopped,
            "v0.61.1",
            "-",
            "/etc/frp/frpc.toml");
    }

    private static string CreateTemporaryPrivateKeyFile()
    {
        using var rsa = RSA.Create(2048);
        var path = Path.Combine(Path.GetTempPath(), "FrpNexusTests", $"ssh-key-{Guid.NewGuid():N}.pem");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, rsa.ExportRSAPrivateKeyPem());
        return path;
    }
}

internal static class KeyboardInteractiveAuthenticationMethodTestExtensions
{
    public static void RaiseAuthenticationPrompt(
        this KeyboardInteractiveAuthenticationMethod method,
        AuthenticationPromptEventArgs args)
    {
        var eventField = typeof(KeyboardInteractiveAuthenticationMethod).GetField(
            "AuthenticationPrompt",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var handler = Assert.IsType<EventHandler<AuthenticationPromptEventArgs>>(eventField?.GetValue(method));

        handler(method, args);
    }
}
