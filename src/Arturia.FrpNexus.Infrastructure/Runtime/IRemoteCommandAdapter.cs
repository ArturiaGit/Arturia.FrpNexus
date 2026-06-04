using Arturia.FrpNexus.Application.Abstractions;
using Arturia.FrpNexus.Core.Models;

namespace Arturia.FrpNexus.Infrastructure.Runtime;

public interface IRemoteCommandAdapter
{
    Task<RemoteCommandResult> ExecuteAsync(
        NodeProfile node,
        SshCredentialReference credential,
        string command,
        CancellationToken cancellationToken = default);
}

public sealed record RemoteCommandResult(
    int ExitCode,
    string Output,
    string Error);
