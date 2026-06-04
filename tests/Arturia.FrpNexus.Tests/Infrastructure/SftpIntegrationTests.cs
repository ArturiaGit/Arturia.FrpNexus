using Arturia.FrpNexus.Application.Abstractions;
using Arturia.FrpNexus.Core.Models;
using Arturia.FrpNexus.Infrastructure.Sftp;

namespace Arturia.FrpNexus.Tests.Infrastructure;

public sealed class SftpIntegrationTests
{
    [Fact]
    public async Task UploadFileAsync_ShouldRunOnlyWhenExplicitlyConfigured()
    {
        var host = Environment.GetEnvironmentVariable("FRPNEXUS_TEST_SSH_HOST");
        var userName = Environment.GetEnvironmentVariable("FRPNEXUS_TEST_SSH_USER");
        var password = Environment.GetEnvironmentVariable("FRPNEXUS_TEST_SSH_PASSWORD");
        var portValue = Environment.GetEnvironmentVariable("FRPNEXUS_TEST_SSH_PORT");
        var remotePath = Environment.GetEnvironmentVariable("FRPNEXUS_TEST_SFTP_REMOTE_PATH");

        if (string.IsNullOrWhiteSpace(host)
            || string.IsNullOrWhiteSpace(userName)
            || string.IsNullOrWhiteSpace(password)
            || string.IsNullOrWhiteSpace(remotePath))
        {
            return;
        }

        var port = int.TryParse(portValue, out var parsedPort) ? parsedPort : 22;
        var node = new NodeProfile(
            "SFTP 集成测试节点",
            host,
            port,
            userName,
            "会话密码",
            "Linux",
            FrpNexusStatus.Pending,
            FrpNexusStatus.Pending,
            "-",
            "-",
            "/etc/frp/frpc.toml");

        var credential = new SshCredentialReference(
            SshAuthenticationMode.SessionPassword,
            SessionPassword: password);

        await using var content = new MemoryStream("frpnexus-sftp-integration-test"u8.ToArray());
        await new SshNetSftpClientAdapter().UploadFileAsync(node, credential, content, remotePath);
    }
}
