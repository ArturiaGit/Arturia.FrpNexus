using Arturia.FrpNexus.Core.Models;

namespace Arturia.FrpNexus.Application.Abstractions;

public interface ISshConnectionService
{
    Task<SshConnectionTestResult> TestConnectionAsync(SshConnectionTestRequest request, CancellationToken cancellationToken = default);
}

public sealed record SshConnectionTestRequest(
    NodeProfile Node,
    SshCredentialReference Credential);

public sealed record SshCredentialReference(
    SshAuthenticationMode AuthenticationMode,
    string? PrivateKeyPath = null,
    string? SessionPassword = null,
    string? PrivateKeyPassphrase = null);

public enum SshAuthenticationMode
{
    PrivateKey,
    SshAgent,
    SessionPassword
}

public sealed record SshConnectionTestResult(
    string NodeName,
    FrpNexusStatus Status,
    DateTimeOffset TestedAt,
    string Message);
