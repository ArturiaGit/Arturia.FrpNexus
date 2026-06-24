using Arturia.FrpNexus.Infrastructure.Ssh;

namespace Arturia.FrpNexus.Tests.Infrastructure;

public sealed class SshNetOperationPolicyTests
{
    [Fact]
    public async Task RunAsync_ShouldReturnOperationResult()
    {
        var result = await SshNetOperationPolicy.RunAsync(
            "test success",
            TimeSpan.FromSeconds(1),
            () => "ok");

        Assert.Equal("ok", result);
    }

    [Fact]
    public async Task RunAsync_ShouldThrowTimeoutExceptionWhenOperationExceedsTimeout()
    {
        var exception = await Assert.ThrowsAsync<TimeoutException>(() =>
            SshNetOperationPolicy.RunAsync(
                "test timeout",
                TimeSpan.FromMilliseconds(20),
                () =>
                {
                    Thread.Sleep(TimeSpan.FromMilliseconds(200));
                    return "late";
                }));

        Assert.Contains("test timeout", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_ShouldThrowOperationCanceledExceptionWhenTokenIsCanceled()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            SshNetOperationPolicy.RunAsync(
                "test cancellation",
                TimeSpan.FromSeconds(1),
                () => "not reached",
                cancellation.Token));
    }

    [Fact]
    public async Task RunAsync_ShouldPreserveOperationException()
    {
        var original = new InvalidOperationException("boom");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            SshNetOperationPolicy.RunAsync<string>(
                "test failure",
                TimeSpan.FromSeconds(1),
                () => throw original));

        Assert.Same(original, exception);
    }
}
