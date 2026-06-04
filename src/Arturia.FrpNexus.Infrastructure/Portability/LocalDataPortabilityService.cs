using System.Text.Json;
using Arturia.FrpNexus.Application.Abstractions;

namespace Arturia.FrpNexus.Infrastructure.Portability;

public sealed class LocalDataPortabilityService(
    ISettingsService settingsService,
    INodeManagementService nodeManagementService,
    ITunnelManagementService tunnelManagementService,
    IConfigurationVersionService configurationVersionService,
    IRuntimeRecordService runtimeRecordService,
    IDeploymentRecordService deploymentRecordService)
    : ILocalDataPortabilityService
{
    private const string CurrentFormatVersion = "frpnexus.local-data.v1";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public async Task<LocalDataExportSnapshot> CreateExportSnapshotAsync(CancellationToken cancellationToken = default)
    {
        return new LocalDataExportSnapshot(
            CurrentFormatVersion,
            DateTimeOffset.UtcNow,
            await settingsService.GetSettingsAsync(cancellationToken),
            await nodeManagementService.ListNodesAsync(cancellationToken),
            await tunnelManagementService.ListTunnelsAsync(cancellationToken),
            await configurationVersionService.ListConfigurationsAsync(cancellationToken),
            await runtimeRecordService.ListRuntimeProcessesAsync(cancellationToken),
            await deploymentRecordService.ListDeploymentRecordsAsync(cancellationToken));
    }

    public async Task ExportAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new InvalidOperationException("导出文件路径不能为空。");
        }

        var snapshot = await CreateExportSnapshotAsync(cancellationToken);
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(filePath);
        await JsonSerializer.SerializeAsync(stream, snapshot, JsonOptions, cancellationToken);
    }

    public async Task ImportAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new InvalidOperationException("导入文件路径不能为空。");
        }

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("导入文件不存在。", filePath);
        }

        await using var stream = File.OpenRead(filePath);
        var snapshot = await JsonSerializer.DeserializeAsync<LocalDataExportSnapshot>(stream, JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("导入文件内容为空或格式无效。");

        if (!string.Equals(snapshot.FormatVersion, CurrentFormatVersion, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("导入文件版本不受支持。");
        }

        await settingsService.SaveSettingsAsync(snapshot.Settings, cancellationToken);

        foreach (var node in snapshot.Nodes)
        {
            await nodeManagementService.SaveNodeAsync(node, cancellationToken);
        }

        foreach (var tunnel in snapshot.Tunnels)
        {
            await tunnelManagementService.SaveTunnelAsync(tunnel, cancellationToken);
        }

        foreach (var configuration in snapshot.Configurations)
        {
            await configurationVersionService.SaveConfigurationAsync(configuration, cancellationToken);
        }

        foreach (var process in snapshot.RuntimeProcesses)
        {
            await runtimeRecordService.SaveRuntimeProcessAsync(process, cancellationToken);
        }

        foreach (var record in snapshot.DeploymentRecords)
        {
            await deploymentRecordService.SaveDeploymentRecordAsync(record, cancellationToken);
        }
    }
}
