using Arturia.FrpNexus.Application.Abstractions;
using Arturia.FrpNexus.Core.Models;
using Arturia.FrpNexus.Infrastructure.Ssh;

namespace Arturia.FrpNexus.Tests.Infrastructure;

public sealed class SshConnectionIntegrationTests
{
    [Fact]
    public async Task ConnectAsync_ShouldRunOnlyWhenExplicitlyConfigured()
    {
        var host = Environment.GetEnvironmentVariable("FRPNEXUS_TEST_SSH_HOST");
        var userName = Environment.GetEnvironmentVariable("FRPNEXUS_TEST_SSH_USER");
        var password = Environment.GetEnvironmentVariable("FRPNEXUS_TEST_SSH_PASSWORD");
        var portValue = Environment.GetEnvironmentVariable("FRPNEXUS_TEST_SSH_PORT");

        if (string.IsNullOrWhiteSpace(host)
            || string.IsNullOrWhiteSpace(userName)
            || string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var port = int.TryParse(portValue, out var parsedPort) ? parsedPort : 22;
        var node = new NodeProfile(
            "集成测试节点",
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

        await new SshNetClientAdapter().ConnectAsync(node, credential);
    }
}
