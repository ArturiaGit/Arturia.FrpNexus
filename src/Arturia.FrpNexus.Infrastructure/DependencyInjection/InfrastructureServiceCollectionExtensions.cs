using Arturia.FrpNexus.Application.Abstractions;
using Arturia.FrpNexus.Infrastructure.Configurations;
using Arturia.FrpNexus.Infrastructure.Deployments;
using Arturia.FrpNexus.Infrastructure.Nodes;
using Arturia.FrpNexus.Infrastructure.Persistence;
using Arturia.FrpNexus.Infrastructure.Runtime;
using Arturia.FrpNexus.Infrastructure.Settings;
using Arturia.FrpNexus.Infrastructure.Ssh;
using Arturia.FrpNexus.Infrastructure.Tunnels;
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
        services.AddSingleton<INodeManagementService, SqliteNodeManagementService>();
        services.AddSingleton<ITunnelManagementService, SqliteTunnelManagementService>();
        services.AddSingleton<IConfigurationVersionService, SqliteConfigurationVersionService>();
        services.AddSingleton<IRuntimeRecordService, SqliteRuntimeRecordService>();
        services.AddSingleton<IDeploymentRecordService, SqliteDeploymentRecordService>();
        services.AddSingleton<ISshClientAdapter, SshNetClientAdapter>();
        services.AddSingleton<ISshConnectionService, SshConnectionService>();

        return services;
    }
}
