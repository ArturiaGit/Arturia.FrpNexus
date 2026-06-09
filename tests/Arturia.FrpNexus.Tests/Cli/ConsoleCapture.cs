namespace Arturia.FrpNexus.Tests.Cli;

internal static class ConsoleCapture
{
    public static async Task<string> CaptureAsync(Func<Task> action)
    {
        var originalOut = Console.Out;
        await using var writer = new StringWriter();

        Console.SetOut(writer);
        try
        {
            await action();
            return writer.ToString();
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    public static async Task<(int ExitCode, string Output)> CaptureAsync(Func<Task<int>> action)
    {
        var originalOut = Console.Out;
        await using var writer = new StringWriter();

        Console.SetOut(writer);
        try
        {
            var exitCode = await action();
            return (exitCode, writer.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }
}
