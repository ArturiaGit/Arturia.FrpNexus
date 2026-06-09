namespace Arturia.FrpNexus.Infrastructure.Configuration;

public sealed class FrpNexusStorageException : Exception
{
    public FrpNexusStorageException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
