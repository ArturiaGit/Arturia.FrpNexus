using Arturia.FrpNexus.Infrastructure.Configuration;

namespace Arturia.FrpNexus.Tests.Configuration;

public sealed class FrpNexusDatabasePathProviderTests
{
    [Fact]
    public void GetDatabasePath_ReturnsFrpNexusDatabaseFileName()
    {
        var provider = new FrpNexusDatabasePathProvider();

        var path = provider.GetDatabasePath();

        Assert.EndsWith(FrpNexusDatabasePathProvider.DatabaseFileName, path, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetDatabasePath_DoesNotCreateDatabaseFile()
    {
        var provider = new FrpNexusDatabasePathProvider();

        var path = provider.GetDatabasePath();
        var existedBefore = File.Exists(path);
        var lengthBefore = existedBefore ? new FileInfo(path).Length : 0;
        var lastWriteTimeBefore = existedBefore ? File.GetLastWriteTimeUtc(path) : DateTime.MinValue;

        _ = provider.GetDatabasePath();

        if (!existedBefore)
        {
            Assert.False(File.Exists(path));
            return;
        }

        var fileInfo = new FileInfo(path);
        Assert.Equal(lengthBefore, fileInfo.Length);
        Assert.Equal(lastWriteTimeBefore, fileInfo.LastWriteTimeUtc);
    }
}
