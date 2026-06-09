using Arturia.FrpNexus.Infrastructure.Configuration;

namespace Arturia.FrpNexus.Tests.Configuration;

public sealed class LiteDbConnectionFactoryTests
{
    [Fact]
    public void Open_WithInvalidPath_ThrowsStorageException()
    {
        var invalidPath = string.Concat(Path.GetInvalidPathChars().FirstOrDefault('\0'), "frpnexus.db");
        if (invalidPath[0] == '\0')
        {
            invalidPath = string.Empty;
        }

        var factory = new LiteDbConnectionFactory(new TemporaryDatabasePathProvider(invalidPath));

        Assert.Throws<FrpNexusStorageException>(() => factory.Open());
    }
}
