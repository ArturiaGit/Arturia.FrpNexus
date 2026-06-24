using System.Runtime.CompilerServices;

namespace Arturia.FrpNexus.Tests.Cli;

public sealed class CliPersistenceStructureTests
{
    [Fact]
    public void CliProgram_ShouldNotRegisterLiteDbPersistence()
    {
        var source = File.ReadAllText(Path.Combine(
            RepositoryRoot,
            "src",
            "Arturia.FrpNexus.Cli",
            "Program.cs"));

        Assert.DoesNotContain("LiteDb", source, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("AddFrpNexusInfrastructure", source);
    }

    [Fact]
    public void InfrastructureProject_ShouldNotReferenceLiteDbPackage()
    {
        var project = File.ReadAllText(Path.Combine(
            RepositoryRoot,
            "src",
            "Arturia.FrpNexus.Infrastructure",
            "Arturia.FrpNexus.Infrastructure.csproj"));

        Assert.DoesNotContain("LiteDB", project, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CliUserFacingText_ShouldDescribeSqlitePersistence()
    {
        var cliFiles = Directory.GetFiles(
            Path.Combine(RepositoryRoot, "src", "Arturia.FrpNexus.Cli"),
            "*.cs",
            SearchOption.AllDirectories);

        var text = string.Join(Environment.NewLine, cliFiles.Select(File.ReadAllText));

        Assert.DoesNotContain("LiteDB", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SQLite", text, StringComparison.OrdinalIgnoreCase);
    }

    private static string RepositoryRoot => FindRepositoryRoot();

    private static string FindRepositoryRoot([CallerFilePath] string sourceFilePath = "")
    {
        var sourceDirectory = Path.GetDirectoryName(sourceFilePath);
        while (!string.IsNullOrWhiteSpace(sourceDirectory))
        {
            if (File.Exists(Path.Combine(sourceDirectory, "Arturia.FrpNexus.sln")))
            {
                return sourceDirectory;
            }

            sourceDirectory = Directory.GetParent(sourceDirectory)?.FullName;
        }

        throw new InvalidOperationException("Unable to locate repository root.");
    }
}
