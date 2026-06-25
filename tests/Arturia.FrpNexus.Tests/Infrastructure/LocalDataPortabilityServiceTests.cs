using System.Text.Json;
using Arturia.FrpNexus.Application.Abstractions;
using Arturia.FrpNexus.Core.Models;
using Arturia.FrpNexus.Infrastructure.Portability;

namespace Arturia.FrpNexus.Tests.Infrastructure;

public sealed class LocalDataPortabilityServiceTests
{
    [Fact]
    public async Task CreateExportSnapshotAsync_ShouldIncludeSafeLocalData()
    {
        var services = CreateServices();
        var service = services.CreatePortabilityService();

        var snapshot = await service.CreateExportSnapshotAsync();

        Assert.Equal("frpnexus.local-data.v1", snapshot.FormatVersion);
        Assert.Equal("GitHub Releases", snapshot.Settings.FrpDownloadSource);
        Assert.Single(snapshot.Nodes);
        Assert.Single(snapshot.Tunnels);
        Assert.Single(snapshot.Configurations);
        Assert.Single(snapshot.RuntimeProcesses);
        Assert.Single(snapshot.DeploymentRecords);
    }

    [Fact]
    public async Task ExportAsync_ShouldWriteJsonFileWithoutSensitiveFields()
    {
        var services = CreateServices();
        var service = services.CreatePortabilityService();
        var exportPath = Path.Combine(Path.GetTempPath(), "FrpNexusTests", Guid.NewGuid().ToString("N"), "backup.json");

        await service.ExportAsync(exportPath);

        Assert.True(File.Exists(exportPath));
        var json = await File.ReadAllTextAsync(exportPath);
        Assert.Contains("frpnexus.local-data.v1", json, StringComparison.Ordinal);
        Assert.DoesNotContain("Password", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Token", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PrivateKey", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Passphrase", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExportAsync_ShouldRedactSecretBearingRuntimeCommandLines()
    {
        var services = CreateServices();
        services.RuntimeProcesses.Clear();
        services.RuntimeProcesses.Add(new RuntimeProcess(
            "frpc-secret",
            "瀵煎嚭鑺傜偣",
            "frpc",
            FrpNexusStatus.Running,
            "2048",
            "1h",
            "127.0.0.1:8080",
            "/opt/frp/frpc --token SECRET_TOKEN --password SECRET_PASSWORD --private-key-passphrase=SECRET_PASSPHRASE -c /etc/frp/frpc.toml"));
        var service = services.CreatePortabilityService();
        var exportPath = Path.Combine(Path.GetTempPath(), "FrpNexusTests", Guid.NewGuid().ToString("N"), "backup.json");

        await service.ExportAsync(exportPath);

        var json = await File.ReadAllTextAsync(exportPath);
        Assert.DoesNotContain("SECRET_TOKEN", json, StringComparison.Ordinal);
        Assert.DoesNotContain("SECRET_PASSWORD", json, StringComparison.Ordinal);
        Assert.DoesNotContain("SECRET_PASSPHRASE", json, StringComparison.Ordinal);
        Assert.Contains("[REDACTED]", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ImportAsync_ShouldUpsertLocalRecords()
    {
        var sourceServices = CreateServices();
        var source = sourceServices.CreatePortabilityService();
        var exportPath = Path.Combine(Path.GetTempPath(), "FrpNexusTests", Guid.NewGuid().ToString("N"), "backup.json");
        await source.ExportAsync(exportPath);

        var targetServices = CreateServices(empty: true);
        targetServices.Nodes.Add(new("淇濈暀鑺傜偣", "203.0.113.99", 22, "deploy", "瀵嗛挜 (KEEP)", "Ubuntu 22.04 LTS", FrpNexusStatus.Online, FrpNexusStatus.Running, "v0.61.1", "1h", "/etc/frp/frpc.toml"));
        var target = targetServices.CreatePortabilityService();

        await target.ImportAsync(exportPath);

        Assert.Contains(targetServices.Nodes, node => node.Name == "淇濈暀鑺傜偣");
        Assert.Contains(targetServices.Nodes, node => node.Name == "瀵煎嚭鑺傜偣");
        Assert.Contains(targetServices.Tunnels, tunnel => tunnel.Name == "瀵煎嚭闅ч亾");
        Assert.Contains(targetServices.Configurations, configuration => configuration.Name == "瀵煎嚭閰嶇疆");
        Assert.Contains(targetServices.RuntimeProcesses, process => process.Name == "瀵煎嚭杩涚▼");
        Assert.Contains(targetServices.DeploymentRecords, record => record.StepName == "瀵煎嚭姝ラ");
    }

    [Fact]
    public async Task ImportAsync_ShouldRejectUnsupportedFormatVersion()
    {
        var exportPath = Path.Combine(Path.GetTempPath(), "FrpNexusTests", Guid.NewGuid().ToString("N"), "backup.json");
        Directory.CreateDirectory(Path.GetDirectoryName(exportPath)!);
        var snapshot = new LocalDataExportSnapshot(
            "frpnexus.local-data.v0",
            DateTimeOffset.UtcNow,
            new FrpNexusSettingsSnapshot("GitHub Releases", "logs", "db"),
            [],
            [],
            [],
            [],
            []);
        await File.WriteAllTextAsync(exportPath, JsonSerializer.Serialize(snapshot));
        var service = CreateServices(empty: true).CreatePortabilityService();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ImportAsync(exportPath));

        Assert.Equal("导入文件版本不受支持。", exception.Message);
    }

    [Fact]
    public void ExportSnapshot_ShouldOnlyExposeSafeFields()
    {
        var propertyNames = typeof(LocalDataExportSnapshot)
            .GetProperties()
            .Select(property => property.Name)
            .ToArray();

        Assert.DoesNotContain(propertyNames, property => property.Contains("Password", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(propertyNames, property => property.Contains("Token", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(propertyNames, property => property.Contains("PrivateKey", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(propertyNames, property => property.Contains("Passphrase", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(propertyNames, property => property.Contains("Log", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(propertyNames, property => property.Contains("Cache", StringComparison.OrdinalIgnoreCase));
    }

    private static ServiceSet CreateServices(bool empty = false)
    {
        var settings = new FrpNexusSettingsSnapshot(
            "GitHub Releases",
            @"D:\FrpNexus\logs",
            @"D:\FrpNexus\data\frpnexus.db");

        return new ServiceSet(
            new FakeSettingsService(settings),
            empty ? [] : [new("瀵煎嚭鑺傜偣", "203.0.113.10", 22, "deploy", "瀵嗛挜 (SAFE_KEY)", "Ubuntu 22.04 LTS", FrpNexusStatus.Online, FrpNexusStatus.Running, "v0.61.1", "2h", "/etc/frp/frpc.toml")],
            empty ? [] : [new("瀵煎嚭闅ч亾", TunnelProtocol.Http, "瀵煎嚭鑺傜偣", "127.0.0.1", 8080, "example.com", FrpNexusStatus.Running, "HTTP 绀轰緥")],
            empty ? [] : [new("瀵煎嚭閰嶇疆", TunnelProtocol.Http, "127.0.0.1", 8080, "example.com", "[[proxies]]", DateTimeOffset.UtcNow)],
            empty ? [] : [new("瀵煎嚭杩涚▼", "瀵煎嚭鑺傜偣", "frpc", FrpNexusStatus.Running, "2048", "1h", "127.0.0.1:8080")],
            empty ? [] : [new("瀵煎嚭姝ラ", "瀵煎嚭鑺傜偣", "閮ㄧ讲姝ラ", FrpNexusStatus.Ready, DateTimeOffset.UtcNow)]);
    }

    private sealed class ServiceSet(
        FakeSettingsService settings,
        List<NodeProfile> nodes,
        List<TunnelProfile> tunnels,
        List<ConfigurationVersion> configurations,
        List<RuntimeProcess> runtimeProcesses,
        List<DeploymentRecord> deploymentRecords)
    {
        public List<NodeProfile> Nodes { get; } = nodes;

        public List<TunnelProfile> Tunnels { get; } = tunnels;

        public List<ConfigurationVersion> Configurations { get; } = configurations;

        public List<RuntimeProcess> RuntimeProcesses { get; } = runtimeProcesses;

        public List<DeploymentRecord> DeploymentRecords { get; } = deploymentRecords;

        public LocalDataPortabilityService CreatePortabilityService()
        {
            return new LocalDataPortabilityService(
                settings,
                new FakeNodeManagementService(Nodes),
                new FakeTunnelManagementService(Tunnels),
                new FakeConfigurationVersionService(Configurations),
                new FakeRuntimeRecordService(RuntimeProcesses),
                new FakeDeploymentRecordService(DeploymentRecords));
        }
    }

    private sealed class FakeSettingsService(FrpNexusSettingsSnapshot initialSettings) : ISettingsService
    {
        private FrpNexusSettingsSnapshot _settings = initialSettings;

        public Task<FrpNexusSettingsSnapshot> GetSettingsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_settings);
        }

        public Task SaveSettingsAsync(FrpNexusSettingsSnapshot settings, CancellationToken cancellationToken = default)
        {
            _settings = settings;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeNodeManagementService(List<NodeProfile> nodes) : INodeManagementService
    {
        public Task<IReadOnlyList<NodeProfile>> ListNodesAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<NodeProfile>>(nodes);

        public Task<NodeProfile?> GetNodeAsync(string nodeName, CancellationToken cancellationToken = default) => Task.FromResult(nodes.FirstOrDefault(node => node.Name == nodeName));

        public Task SaveNodeAsync(NodeProfile node, CancellationToken cancellationToken = default)
        {
            nodes.RemoveAll(item => item.Name == node.Name);
            nodes.Add(node);
            return Task.CompletedTask;
        }

        public Task DeleteNodeAsync(string nodeName, CancellationToken cancellationToken = default)
        {
            nodes.RemoveAll(item => item.Name == nodeName);
            return Task.CompletedTask;
        }

        public Task UpdateLastConnectionAsync(string nodeName, DateTimeOffset connectedAt, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task UpdateConnectionTestResultAsync(string nodeName, FrpNexusStatus status, DateTimeOffset testedAt, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeTunnelManagementService(List<TunnelProfile> tunnels) : ITunnelManagementService
    {
        public Task<IReadOnlyList<TunnelProfile>> ListTunnelsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<TunnelProfile>>(tunnels);

        public Task<TunnelProfile?> GetTunnelAsync(string tunnelName, CancellationToken cancellationToken = default) => Task.FromResult(tunnels.FirstOrDefault(tunnel => tunnel.Name == tunnelName));

        public Task SaveTunnelAsync(TunnelProfile tunnel, CancellationToken cancellationToken = default)
        {
            tunnels.RemoveAll(item => item.Name == tunnel.Name);
            tunnels.Add(tunnel);
            return Task.CompletedTask;
        }

        public Task DeleteTunnelAsync(string tunnelName, CancellationToken cancellationToken = default)
        {
            tunnels.RemoveAll(item => item.Name == tunnelName);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeConfigurationVersionService(List<ConfigurationVersion> configurations) : IConfigurationVersionService
    {
        public Task<IReadOnlyList<ConfigurationVersion>> ListConfigurationsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ConfigurationVersion>>(configurations);

        public Task<ConfigurationVersion?> GetConfigurationAsync(string name, CancellationToken cancellationToken = default) => Task.FromResult(configurations.FirstOrDefault(configuration => configuration.Name == name));

        public Task SaveConfigurationAsync(ConfigurationVersion configuration, CancellationToken cancellationToken = default)
        {
            configurations.RemoveAll(item => item.Name == configuration.Name);
            configurations.Add(configuration);
            return Task.CompletedTask;
        }

        public Task DeleteConfigurationAsync(string name, CancellationToken cancellationToken = default)
        {
            configurations.RemoveAll(item => item.Name == name);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeRuntimeRecordService(List<RuntimeProcess> runtimeProcesses) : IRuntimeRecordService
    {
        public Task<IReadOnlyList<RuntimeProcess>> ListRuntimeProcessesAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<RuntimeProcess>>(runtimeProcesses);

        public Task<RuntimeProcess?> GetRuntimeProcessAsync(string processName, CancellationToken cancellationToken = default) => Task.FromResult(runtimeProcesses.FirstOrDefault(process => process.Name == processName));

        public Task SaveRuntimeProcessAsync(RuntimeProcess process, CancellationToken cancellationToken = default)
        {
            runtimeProcesses.RemoveAll(item => item.Name == process.Name);
            runtimeProcesses.Add(process);
            return Task.CompletedTask;
        }

        public Task DeleteRuntimeProcessAsync(string processName, CancellationToken cancellationToken = default)
        {
            runtimeProcesses.RemoveAll(item => item.Name == processName);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeDeploymentRecordService(List<DeploymentRecord> deploymentRecords) : IDeploymentRecordService
    {
        public Task<IReadOnlyList<DeploymentRecord>> ListDeploymentRecordsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<DeploymentRecord>>(deploymentRecords);

        public Task<DeploymentRecord?> GetDeploymentRecordAsync(string stepName, CancellationToken cancellationToken = default) => Task.FromResult(deploymentRecords.FirstOrDefault(record => record.StepName == stepName));

        public Task SaveDeploymentRecordAsync(DeploymentRecord record, CancellationToken cancellationToken = default)
        {
            deploymentRecords.RemoveAll(item => item.StepName == record.StepName);
            deploymentRecords.Add(record);
            return Task.CompletedTask;
        }

        public Task DeleteDeploymentRecordAsync(string stepName, CancellationToken cancellationToken = default)
        {
            deploymentRecords.RemoveAll(record => record.StepName == stepName);
            return Task.CompletedTask;
        }
    }
}
