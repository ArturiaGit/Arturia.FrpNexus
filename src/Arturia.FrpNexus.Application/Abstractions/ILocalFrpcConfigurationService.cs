namespace Arturia.FrpNexus.Application.Abstractions;

public interface ILocalFrpcConfigurationService
{
    Task<LocalFrpcConfigurationSnapshot> GetConfigurationAsync(
        string nodeName,
        CancellationToken cancellationToken = default);

    Task SaveFrpcBinaryPathAsync(
        string frpcBinaryPath,
        CancellationToken cancellationToken = default);

    Task SaveNodeConfigPathAsync(
        string nodeName,
        string frpcConfigPath,
        CancellationToken cancellationToken = default);

    string GetDefaultNodeConfigPath(string nodeName);
}

public sealed record LocalFrpcConfigurationSnapshot(
    string FrpcBinaryPath,
    string FrpcConfigPath,
    string SuggestedFrpcConfigPath);
