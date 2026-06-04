namespace Arturia.FrpNexus.Application.Abstractions;

public interface IFrpReleaseService
{
    Task<IReadOnlyList<FrpReleaseVersion>> ListAvailableVersionsAsync(CancellationToken cancellationToken = default);

    Task<FrpReleasePreparationResult> PrepareReleaseAsync(
        FrpReleasePreparationRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record FrpReleaseVersion(
    string Version,
    DateTimeOffset PublishedAt);

public sealed record FrpReleasePreparationRequest(
    string Version,
    string TargetRuntime,
    string BinaryName);

public sealed record FrpReleasePreparationResult(
    string Version,
    string TargetRuntime,
    string BinaryName,
    string LocalPath,
    DateTimeOffset PreparedAt,
    string Message);
