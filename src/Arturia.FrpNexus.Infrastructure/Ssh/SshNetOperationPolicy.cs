namespace Arturia.FrpNexus.Infrastructure.Ssh;

internal static class SshNetOperationPolicy
{
    public static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(15);
    public static readonly TimeSpan RemoteCommandTimeout = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan SftpOperationTimeout = TimeSpan.FromSeconds(60);
    public static readonly TimeSpan SftpUploadTimeout = TimeSpan.FromMinutes(5);

    public static async Task RunAsync(
        string operationName,
        TimeSpan timeout,
        Action operation,
        CancellationToken cancellationToken = default)
    {
        await RunAsync(
            operationName,
            timeout,
            () =>
            {
                operation();
                return true;
            },
            cancellationToken);
    }

    public static async Task<T> RunAsync<T>(
        string operationName,
        TimeSpan timeout,
        Func<T> operation,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // SSH.NET exposes synchronous APIs. Cancellation here stops the caller from
        // waiting, but it cannot forcibly abort a socket operation already inside
        // SSH.NET; ConnectionInfo.Timeout and per-operation timeouts where available
        // bound that work.
        var operationTask = Task.Run(operation);
        try
        {
            return await operationTask.WaitAsync(timeout, cancellationToken);
        }
        catch (TimeoutException exception)
        {
            throw new TimeoutException($"{operationName} 超时。", exception);
        }
    }
}
