using Arturia.FrpNexus.Application.Abstractions;
using Arturia.FrpNexus.Desktop.ViewModels.Nodes;

namespace Arturia.FrpNexus.Tests.Desktop;

public sealed class NodeCredentialWorkflowTests
{
    [Fact]
    public async Task CreateCredentialAsync_ShouldUseTypedSessionPassword()
    {
        var workflow = new NodeCredentialWorkflow(new FakeNodeCredentialSecretService());

        var result = await workflow.CreateCredentialAsync(new NodeCredentialInput(
            "node-a",
            "SessionPassword",
            " typed-password ",
            null,
            null));

        Assert.True(result.Success);
        Assert.Equal(SshAuthenticationMode.SessionPassword, result.Credential.AuthenticationMode);
        Assert.Equal(" typed-password ", result.Credential.SessionPassword);
    }

    [Fact]
    public async Task CreateCredentialAsync_ShouldUseSavedSessionPasswordWhenTypedPasswordIsBlank()
    {
        var secretService = new FakeNodeCredentialSecretService();
        secretService.SessionPasswords["node-a"] = "saved-password";
        var workflow = new NodeCredentialWorkflow(secretService);

        var result = await workflow.CreateCredentialAsync(new NodeCredentialInput(
            "node-a",
            "SessionPassword",
            string.Empty,
            null,
            null));

        Assert.True(result.Success);
        Assert.Equal("saved-password", result.Credential.SessionPassword);
    }

    [Fact]
    public async Task CreateCredentialAsync_ShouldReturnWarningWhenSessionPasswordIsMissing()
    {
        var workflow = new NodeCredentialWorkflow(new FakeNodeCredentialSecretService());

        var result = await workflow.CreateCredentialAsync(new NodeCredentialInput(
            "node-a",
            "SessionPassword",
            string.Empty,
            null,
            null));

        Assert.False(result.Success);
        Assert.Equal("warning", result.StatusSeverity);
    }

    [Fact]
    public async Task CreateCredentialAsync_ShouldCreatePrivateKeyCredential()
    {
        var workflow = new NodeCredentialWorkflow(new FakeNodeCredentialSecretService());

        var result = await workflow.CreateCredentialAsync(new NodeCredentialInput(
            "node-a",
            "PrivateKey",
            null,
            " C:\\keys\\node-a ",
            "passphrase"));

        Assert.True(result.Success);
        Assert.Equal(SshAuthenticationMode.PrivateKey, result.Credential.AuthenticationMode);
        Assert.Equal("C:\\keys\\node-a", result.Credential.PrivateKeyPath);
        Assert.Equal("passphrase", result.Credential.PrivateKeyPassphrase);
    }

    [Fact]
    public async Task SaveRememberedSessionPasswordIfNeededAsync_ShouldOnlySaveExplicitSessionPasswordOptIn()
    {
        var secretService = new FakeNodeCredentialSecretService();
        var workflow = new NodeCredentialWorkflow(secretService);

        var skipped = await workflow.SaveRememberedSessionPasswordIfNeededAsync(
            "node-a",
            isSessionPasswordMode: false,
            rememberSessionPassword: true,
            typedSessionPassword: "private-key-secret");
        var saved = await workflow.SaveRememberedSessionPasswordIfNeededAsync(
            "node-a",
            isSessionPasswordMode: true,
            rememberSessionPassword: true,
            typedSessionPassword: "session-secret");

        Assert.False(skipped.HasSavedSessionPassword);
        Assert.True(saved.HasSavedSessionPassword);
        Assert.Equal("session-secret", secretService.SessionPasswords["node-a"]);
    }

    [Fact]
    public async Task GetSavedSessionPasswordStateAsync_ShouldClearRememberFlagWhenNoSecretExists()
    {
        var workflow = new NodeCredentialWorkflow(new FakeNodeCredentialSecretService());

        var state = await workflow.GetSavedSessionPasswordStateAsync("node-a");

        Assert.False(state.HasSavedSessionPassword);
        Assert.True(state.ShouldClearRememberSessionPassword);
    }

    [Fact]
    public async Task DeleteSessionPasswordAsync_ShouldDeleteStoredSecret()
    {
        var secretService = new FakeNodeCredentialSecretService();
        secretService.SessionPasswords["node-a"] = "saved-password";
        var workflow = new NodeCredentialWorkflow(secretService);

        await workflow.DeleteSessionPasswordAsync("node-a");

        Assert.False(secretService.SessionPasswords.ContainsKey("node-a"));
    }

    private sealed class FakeNodeCredentialSecretService : INodeCredentialSecretService
    {
        public Dictionary<string, string> SessionPasswords { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Task<bool> HasSessionPasswordAsync(string nodeName, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(SessionPasswords.ContainsKey(nodeName));
        }

        public Task<string?> GetSessionPasswordAsync(string nodeName, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(SessionPasswords.TryGetValue(nodeName, out var password) ? password : null);
        }

        public Task SaveSessionPasswordAsync(string nodeName, string sessionPassword, CancellationToken cancellationToken = default)
        {
            SessionPasswords[nodeName] = sessionPassword;
            return Task.CompletedTask;
        }

        public Task DeleteSessionPasswordAsync(string nodeName, CancellationToken cancellationToken = default)
        {
            SessionPasswords.Remove(nodeName);
            return Task.CompletedTask;
        }
    }
}
