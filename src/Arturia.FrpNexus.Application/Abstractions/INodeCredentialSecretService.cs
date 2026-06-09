namespace Arturia.FrpNexus.Application.Abstractions;

public interface INodeCredentialSecretService
{
    Task<bool> HasSessionPasswordAsync(
        string nodeName,
        CancellationToken cancellationToken = default);

    Task<string?> GetSessionPasswordAsync(
        string nodeName,
        CancellationToken cancellationToken = default);

    Task SaveSessionPasswordAsync(
        string nodeName,
        string password,
        CancellationToken cancellationToken = default);

    Task DeleteSessionPasswordAsync(
        string nodeName,
        CancellationToken cancellationToken = default);
}
