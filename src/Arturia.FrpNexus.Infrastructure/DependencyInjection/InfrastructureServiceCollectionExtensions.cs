using Arturia.FrpNexus.Application.Abstractions;
using Arturia.FrpNexus.Infrastructure.Persistence;
using Arturia.FrpNexus.Infrastructure.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace Arturia.FrpNexus.Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddFrpNexusInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IFrpNexusDatabasePathProvider, FrpNexusDatabasePathProvider>();
        services.AddSingleton<ISqliteConnectionFactory, SqliteConnectionFactory>();
        services.AddSingleton<ISqliteDatabaseInitializer, SqliteDatabaseInitializer>();
        services.AddSingleton<ISettingsService, SqliteSettingsService>();

        return services;
    }
}
