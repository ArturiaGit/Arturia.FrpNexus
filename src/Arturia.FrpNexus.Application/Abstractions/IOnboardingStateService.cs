namespace Arturia.FrpNexus.Application.Abstractions;

public interface IOnboardingStateService
{
    Task<OnboardingStateSnapshot> GetStateAsync(CancellationToken cancellationToken = default);

    Task AcceptCurrentDisclaimerAsync(
        DateTimeOffset acceptedAt,
        CancellationToken cancellationToken = default);
}
