namespace Arturia.FrpNexus.Infrastructure.Releases;

public interface IFrpReleaseCachePathProvider
{
    string GetReleaseCacheDirectory();
}

public sealed class FrpReleaseCachePathProvider : IFrpReleaseCachePathProvider
{
    public string GetReleaseCacheDirectory()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "Arturia", "FrpNexus", "core", "releases");
    }
}
