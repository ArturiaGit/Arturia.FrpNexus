using Arturia.FrpNexus.Application.Abstractions;
using Arturia.FrpNexus.Core.Models;

namespace Arturia.FrpNexus.Infrastructure.Ssh;

public interface ISshClientAdapter
{
    Task ConnectAsync(
        NodeProfile node,
        SshCredentialReference credential,
        CancellationToken cancellationToken = default);

    Task<ISshClientSession> OpenSessionAsync(
        NodeProfile node,
        SshCredentialReference credential,
        CancellationToken cancellationToken = default);
}

public interface ISshClientSession : IDisposable
{
    bool IsConnected { get; }

    void Disconnect();
}
