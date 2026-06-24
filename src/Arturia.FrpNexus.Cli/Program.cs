using Cocona;
using Microsoft.Extensions.DependencyInjection;
using Arturia.FrpNexus.Cli.Commands;
using Arturia.FrpNexus.Application.ExcaliburTunnel;
using Arturia.FrpNexus.Core.AvalonDaemon;
using Arturia.FrpNexus.Core.Configuration;
using Arturia.FrpNexus.Core.ExcaliburTunnel;
using Arturia.FrpNexus.Core.InvisibleAirService;
using Arturia.FrpNexus.Infrastructure.AvalonDaemon;
using Arturia.FrpNexus.Infrastructure.Configuration;
using Arturia.FrpNexus.Infrastructure.DependencyInjection;
using Arturia.FrpNexus.Infrastructure.ExcaliburTunnel;
using Arturia.FrpNexus.Infrastructure.InvisibleAirService;

namespace Arturia.FrpNexus.Cli;

internal static class Program
{
    public static void Main(string[] args)
    {
        var builder = CoconaApp.CreateBuilder(args);

        builder.Services.AddFrpNexusInfrastructure();
        builder.Services.AddSingleton<IAvalonDaemon, FrpAvalonDaemon>();
        builder.Services.AddSingleton<IExcaliburTunnel, FrpExcaliburTunnel>();
        builder.Services.AddSingleton<IFrpNexusSettingsStore, SqliteFrpNexusSettingsStore>();
        builder.Services.AddSingleton<ITunnelProfileRepository, SqliteTunnelProfileRepository>();
        builder.Services.AddSingleton<TunnelProfileService>();
        builder.Services.AddSingleton<SystemdServiceUnitBuilder>();
        builder.Services.AddSingleton<IInvisibleAirService, LinuxInvisibleAirService>();

        var app = builder.Build();

        app.AddCommands<RootCommands>();
        app.AddSubCommand("daemon", daemon => daemon.AddCommands<DaemonCommands>());
        app.AddSubCommand("tunnel", tunnel => tunnel.AddCommands<TunnelCommands>());
        app.AddSubCommand("service", service => service.AddCommands<ServiceCommands>());
        app.AddSubCommand("config", config => config.AddCommands<ConfigCommands>());
        app.AddSubCommand("profile", profile => profile.AddCommands<ProfileCommands>());

        app.Run();
    }
}
