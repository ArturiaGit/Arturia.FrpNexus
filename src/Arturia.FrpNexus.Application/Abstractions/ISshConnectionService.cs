namespace Arturia.FrpNexus.Application.Abstractions;

public interface ISshConnectionService
{
    Task TestConnectionAsync(string nodeName, CancellationToken cancellationToken = default);
}
