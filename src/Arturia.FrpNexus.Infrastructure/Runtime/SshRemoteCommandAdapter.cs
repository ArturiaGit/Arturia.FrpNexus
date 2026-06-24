using Arturia.FrpNexus.Application.Abstractions;
using Arturia.FrpNexus.Core.Models;
using Arturia.FrpNexus.Infrastructure.Ssh;
using Renci.SshNet;

namespace Arturia.FrpNexus.Infrastructure.Runtime;

public sealed class SshRemoteCommandAdapter : IRemoteCommandAdapter
{
    public Task<RemoteCommandResult> ExecuteAsync(
        NodeProfile node,
        SshCredentialReference credential,
        string command,
        CancellationToken cancellationToken = default)
    {
        return SshNetOperationPolicy.RunAsync(
            "远程命令执行",
            SshNetOperationPolicy.RemoteCommandTimeout,
            () =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var client = new SshClient(SshConnectionInfoFactory.Create(node, credential, "远程命令"));
            client.Connect();

            cancellationToken.ThrowIfCancellationRequested();
            using var sshCommand = client.CreateCommand(command);
            sshCommand.CommandTimeout = SshNetOperationPolicy.RemoteCommandTimeout;
            var output = sshCommand.Execute();
            var error = sshCommand.Error;
            var exitCode = sshCommand.ExitStatus ?? -1;

            client.Disconnect();
            return new RemoteCommandResult(exitCode, output, error);
        }, cancellationToken);
    }
}
