namespace Arturia.FrpNexus.Application.Abstractions;

public sealed record OnboardingStateSnapshot(
    string CurrentDisclaimerVersion,
    string? AcceptedDisclaimerVersion,
    DateTimeOffset? AcceptedAt)
{
    public bool IsCurrentDisclaimerAccepted => string.Equals(
        AcceptedDisclaimerVersion,
        CurrentDisclaimerVersion,
        StringComparison.Ordinal);
}
