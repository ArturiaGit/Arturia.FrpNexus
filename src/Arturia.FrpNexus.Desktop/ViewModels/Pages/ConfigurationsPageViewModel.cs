using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Arturia.FrpNexus.Application.Abstractions;
using Arturia.FrpNexus.Core.Models;
using Arturia.FrpNexus.Desktop.Services;
using Arturia.FrpNexus.Desktop.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arturia.FrpNexus.Desktop.ViewModels.Pages;

public sealed partial class ConfigurationsPageViewModel : PageViewModel
{
    private const string DefaultServerBindPort = "7000";
    private const string DefaultServerConfigPath = "/opt/frp/frps.toml";

    private static readonly Regex TomlAssignmentRegex = new(
        @"^(\s*)([A-Za-z0-9_.-]+)(\s*=\s*)(.*)$",
        RegexOptions.Compiled);

    private static readonly Regex TomlValueTokenRegex = new(
        @"""(?:\\""|[^""])*""|\b\d+\b",
        RegexOptions.Compiled);

    private readonly ITomlConfigurationService _tomlConfigurationService;
    private readonly INodeManagementService _nodeManagementService;
    private readonly ITunnelManagementService _tunnelManagementService;
    private readonly INodeConnectionSessionService _nodeConnectionSessionService;
    private readonly IRemoteFileTransferService _remoteFileTransferService;
    private readonly IClipboardService _clipboardService;

    [ObservableProperty]
    private NodeProfile? _selectedTargetNode;

    [ObservableProperty]
    private string _tomlPreview = string.Empty;

    [ObservableProperty]
    private string _statusText = "请选择目标节点后生成本地 frpc.toml 预览。";

    [ObservableProperty]
    private string _errorText = string.Empty;

    [ObservableProperty]
    private string _previewActionStatusText = "生成 frpc.toml 后可在此验证或复制。";

    [ObservableProperty]
    private string _clientTunnelCountText = "共 0 条隧道";

    [ObservableProperty]
    private string _clientConfigSourceText = "frpc.toml 将由【节点】页的服务器地址和【隧道】页的代理规则生成。";

    [ObservableProperty]
    private bool _isAdvancedOptionsVisible;

    [ObservableProperty]
    private string _remoteConfigPath = string.Empty;

    [ObservableProperty]
    private string _serverBindPort = string.Empty;

    [ObservableProperty]
    private string _serverTomlPreview = string.Empty;

    [ObservableProperty]
    private string _serverUploadStatusText = "远程 frps.toml 尚未生成。";

    [ObservableProperty]
    private string _serverUploadErrorText = string.Empty;

    [ObservableProperty]
    private string _targetNodeCountText = "共 0 个目标节点";

    [ObservableProperty]
    private bool _isUploadingServerToml;

    public ConfigurationsPageViewModel(
        ITomlConfigurationService tomlConfigurationService,
        INodeManagementService nodeManagementService,
        ITunnelManagementService tunnelManagementService,
        INodeConnectionSessionService nodeConnectionSessionService,
        IRemoteFileTransferService remoteFileTransferService,
        IClipboardService clipboardService)
        : base("配置", "查看本地 frpc.toml 生成结果，并上传远程 VPS 的 frps.toml")
    {
        _tomlConfigurationService = tomlConfigurationService;
        _nodeManagementService = nodeManagementService;
        _tunnelManagementService = tunnelManagementService;
        _nodeConnectionSessionService = nodeConnectionSessionService;
        _remoteFileTransferService = remoteFileTransferService;
        _clipboardService = clipboardService;
        TargetNodes = [];
        ClientTunnels = [];
        TomlPreviewLines = [];
        RefreshTomlPreviewLines();
        _ = LoadTargetNodesAsync();
    }

    public ObservableCollection<NodeProfile> TargetNodes { get; }

    public ObservableCollection<TunnelProfile> ClientTunnels { get; }

    public ObservableCollection<TomlPreviewLineViewModel> TomlPreviewLines { get; }

    public string AdvancedOptionsChevronIcon => IsAdvancedOptionsVisible ? "chevron_down" : "chevron_right";

    public double AdvancedOptionsPanelMaxHeight => IsAdvancedOptionsVisible ? 112 : 0;

    public double AdvancedOptionsPanelOpacity => IsAdvancedOptionsVisible ? 1 : 0;

    public double AdvancedOptionsPanelOffsetY => IsAdvancedOptionsVisible ? 0 : -4;

    public bool IsAdvancedOptionsInteractive => IsAdvancedOptionsVisible;

    public bool IsServerTomlPreviewVisible => !string.IsNullOrWhiteSpace(ServerTomlPreview);

    public string ResolvedRemoteConfigPathText => SelectedTargetNode is null
        ? DefaultServerConfigPath
        : ResolveNodeConfigPath(SelectedTargetNode);

    [RelayCommand]
    public async Task LoadTargetNodesAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<NodeProfile> nodes;
        try
        {
            nodes = await _nodeManagementService.ListNodesAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            StatusText = "目标节点加载已取消。";
            ServerUploadStatusText = "目标节点加载已取消。";
            return;
        }
        catch (Exception ex)
        {
            TargetNodeCountText = "目标节点加载失败";
            StatusText = ViewModelErrorText.ForUser("目标节点加载", ex);
            ServerUploadStatusText = StatusText;
            return;
        }

        TargetNodes.Clear();
        foreach (var node in nodes)
        {
            TargetNodes.Add(node);
        }

        TargetNodeCountText = $"共 {TargetNodes.Count} 个目标节点";

        if (SelectedTargetNode is null && TargetNodes.Count > 0)
        {
            SelectedTargetNode = TargetNodes[0];
        }
    }

    [RelayCommand]
    private async Task GenerateTomlAsync(CancellationToken cancellationToken = default)
    {
        var node = SelectedTargetNode;
        if (node is null)
        {
            ErrorText = "请先选择目标节点。";
            StatusText = "frpc.toml 生成失败。";
            return;
        }

        IReadOnlyList<TunnelProfile> tunnels;
        try
        {
            tunnels = await LoadClientTunnelsForNodeAsync(node, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            ErrorText = "frpc.toml 生成已取消。";
            StatusText = "frpc.toml 生成失败。";
            return;
        }
        catch (Exception ex)
        {
            ErrorText = ViewModelErrorText.ForUser("隧道配置加载", ex);
            StatusText = "frpc.toml 生成失败。";
            return;
        }

        if (tunnels.Count == 0)
        {
            TomlPreview = string.Empty;
            ErrorText = "当前节点没有可用于 frpc.toml 的隧道，请先到【隧道】页新增代理规则。";
            StatusText = "frpc.toml 生成失败。";
            return;
        }

        try
        {
            TomlPreview = _tomlConfigurationService.GenerateClientToml(node, tunnels);
            ErrorText = string.Empty;
            StatusText = $"已生成 {node.Name} 的本地 frpc.toml 预览，包含 {tunnels.Count} 条隧道。";
        }
        catch (InvalidOperationException ex)
        {
            ErrorText = ex.Message;
            StatusText = "frpc.toml 生成失败。";
        }
        catch (Exception ex)
        {
            ErrorText = ViewModelErrorText.ForUser("frpc.toml 生成", ex);
            StatusText = "frpc.toml 生成失败。";
        }
    }

    [RelayCommand]
    private async Task RefreshClientTunnelsAsync(CancellationToken cancellationToken = default)
    {
        var node = SelectedTargetNode;
        if (node is null)
        {
            ClientTunnels.Clear();
            ClientTunnelCountText = "共 0 条隧道";
            ClientConfigSourceText = "请选择目标节点后查看关联隧道。";
            return;
        }

        try
        {
            await LoadClientTunnelsForNodeAsync(node, cancellationToken);
            ErrorText = string.Empty;
            StatusText = $"已刷新 {node.Name} 的隧道来源。";
        }
        catch (OperationCanceledException)
        {
            ErrorText = "隧道刷新已取消。";
            StatusText = "隧道刷新失败。";
        }
        catch (Exception ex)
        {
            ErrorText = ViewModelErrorText.ForUser("隧道刷新", ex);
            StatusText = "隧道刷新失败。";
        }
    }

    [RelayCommand]
    private async Task ValidateTomlAsync()
    {
        try
        {
            await _tomlConfigurationService.ValidateAsync(TomlPreview);
            ErrorText = string.Empty;
            StatusText = "TOML 本地校验通过。";
            PreviewActionStatusText = "语法校验通过";
        }
        catch (InvalidOperationException ex)
        {
            ErrorText = ex.Message;
            StatusText = "TOML 本地校验失败。";
            PreviewActionStatusText = $"语法校验失败：{ex.Message}";
        }
        catch (OperationCanceledException)
        {
            ErrorText = "TOML 本地校验已取消。";
            StatusText = "TOML 本地校验失败。";
            PreviewActionStatusText = "语法校验已取消";
        }
        catch (Exception ex)
        {
            ErrorText = ViewModelErrorText.ForUser("TOML 本地校验", ex);
            StatusText = "TOML 本地校验失败。";
            PreviewActionStatusText = ViewModelErrorText.ForUser("TOML 语法校验", ex);
        }
    }

    [RelayCommand]
    private async Task CopyTomlAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(TomlPreview))
        {
            PreviewActionStatusText = "没有可复制的 TOML 内容";
            return;
        }

        try
        {
            await _clipboardService.SetTextAsync(TomlPreview, cancellationToken);
            PreviewActionStatusText = "已复制 frpc.toml 内容";
        }
        catch (OperationCanceledException)
        {
            PreviewActionStatusText = "复制已取消";
        }
        catch (Exception ex)
        {
            PreviewActionStatusText = ViewModelErrorText.ForUser("复制 TOML", ex);
        }
    }

    [RelayCommand]
    private void GenerateServerToml()
    {
        if (!TryGenerateServerToml(out var toml))
        {
            return;
        }

        ServerTomlPreview = toml;
        ServerUploadErrorText = string.Empty;
        ServerUploadStatusText = "已生成远程 frps.toml，尚未上传到 VPS。";
    }

    [RelayCommand]
    private async Task UploadServerTomlAsync(CancellationToken cancellationToken = default)
    {
        var node = SelectedTargetNode;
        if (node is null)
        {
            ServerUploadErrorText = "请先选择目标节点。";
            ServerUploadStatusText = "frps.toml 上传失败。";
            return;
        }

        var remotePath = ResolveNodeConfigPath(node);
        if (!remotePath.StartsWith("/", StringComparison.Ordinal))
        {
            ServerUploadErrorText = "远程配置路径必须是 Linux 绝对路径，例如 /etc/frp/frps.toml。";
            ServerUploadStatusText = "frps.toml 上传失败。";
            return;
        }

        var session = _nodeConnectionSessionService.GetSessionStatus(node.Name);
        if (session.State != NodeConnectionSessionState.Online)
        {
            ServerUploadErrorText = "目标节点 SSH 会话未在线，请先在【节点】页连接 VPS。";
            ServerUploadStatusText = "frps.toml 上传失败。";
            return;
        }

        var credential = _nodeConnectionSessionService.GetConnectedCredential(node.Name);
        if (credential is null)
        {
            ServerUploadErrorText = "未找到可用的 SSH 会话凭据，请重新连接目标节点。";
            ServerUploadStatusText = "frps.toml 上传失败。";
            return;
        }

        if (string.IsNullOrWhiteSpace(ServerTomlPreview) && !TryGenerateServerToml(out _))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(ServerTomlPreview))
        {
            ServerUploadErrorText = "远程 frps.toml 内容不能为空。";
            ServerUploadStatusText = "frps.toml 上传失败。";
            return;
        }

        IsUploadingServerToml = true;
        ServerUploadErrorText = string.Empty;
        ServerUploadStatusText = "正在上传 frps.toml...";

        try
        {
            var result = await _remoteFileTransferService.UploadConfigurationAsync(
                new RemoteConfigurationUploadRequest(node, credential, ServerTomlPreview, remotePath),
                cancellationToken);

            if (result.Status is FrpNexusStatus.Error)
            {
                ServerUploadErrorText = result.Message;
                ServerUploadStatusText = "frps.toml 上传失败。";
                return;
            }

            ServerUploadStatusText = $"已上传 frps.toml 到 {result.RemotePath}";
        }
        catch (OperationCanceledException)
        {
            ServerUploadErrorText = "frps.toml 上传已取消。";
            ServerUploadStatusText = "frps.toml 上传失败。";
        }
        catch (Exception ex)
        {
            ServerUploadErrorText = ViewModelErrorText.ForUser("frps.toml 上传", ex);
            ServerUploadStatusText = "frps.toml 上传失败。";
        }
        finally
        {
            IsUploadingServerToml = false;
        }
    }

    [RelayCommand]
    private void ToggleAdvancedOptions()
    {
        IsAdvancedOptionsVisible = !IsAdvancedOptionsVisible;
    }

    partial void OnSelectedTargetNodeChanged(NodeProfile? value)
    {
        ServerUploadErrorText = string.Empty;
        ServerUploadStatusText = value is null
            ? "请选择目标节点后上传 frps.toml。"
            : $"目标路径来自节点远程 FRP 目录：{ResolveNodeConfigPath(value)}";
        RemoteConfigPath = string.Empty;
        TomlPreview = string.Empty;
        ErrorText = string.Empty;
        StatusText = value is null
            ? "请选择目标节点后生成本地 frpc.toml 预览。"
            : $"已选择 {value.Name}，请生成本地 frpc.toml 预览。";
        OnPropertyChanged(nameof(ResolvedRemoteConfigPathText));
        _ = RefreshClientTunnelsAsync();
    }

    partial void OnServerBindPortChanged(string value)
    {
        ServerUploadErrorText = string.Empty;
        ServerUploadStatusText = "服务端端口已修改，请重新生成 frps.toml。";
    }

    partial void OnServerTomlPreviewChanged(string value)
    {
        OnPropertyChanged(nameof(IsServerTomlPreviewVisible));
    }

    partial void OnTomlPreviewChanged(string value)
    {
        RefreshTomlPreviewLines();
        PreviewActionStatusText = string.IsNullOrWhiteSpace(value)
            ? "生成 frpc.toml 后可在此验证或复制。"
            : "frpc.toml 已更新，可验证语法或复制内容。";
    }

    partial void OnIsAdvancedOptionsVisibleChanged(bool value)
    {
        OnPropertyChanged(nameof(AdvancedOptionsChevronIcon));
        OnPropertyChanged(nameof(AdvancedOptionsPanelMaxHeight));
        OnPropertyChanged(nameof(AdvancedOptionsPanelOpacity));
        OnPropertyChanged(nameof(AdvancedOptionsPanelOffsetY));
        OnPropertyChanged(nameof(IsAdvancedOptionsInteractive));
    }

    private bool TryGenerateServerToml(out string toml)
    {
        toml = string.Empty;

        var bindPortText = string.IsNullOrWhiteSpace(ServerBindPort)
            ? DefaultServerBindPort
            : ServerBindPort.Trim();

        if (!int.TryParse(bindPortText, out var bindPort))
        {
            ServerUploadErrorText = "frps 监听端口必须是 1 到 65535 之间的数字。";
            ServerUploadStatusText = "frps.toml 生成失败。";
            return false;
        }

        try
        {
            toml = _tomlConfigurationService.GenerateServerToml(bindPort);
            ServerTomlPreview = toml;
            return true;
        }
        catch (InvalidOperationException ex)
        {
            ServerUploadErrorText = ex.Message;
            ServerUploadStatusText = "frps.toml 生成失败。";
            return false;
        }
        catch (Exception ex)
        {
            ServerUploadErrorText = ViewModelErrorText.ForUser("frps.toml 生成", ex);
            ServerUploadStatusText = "frps.toml 生成失败。";
            return false;
        }
    }

    private static string ResolveNodeConfigPath(NodeProfile node)
    {
        return string.IsNullOrWhiteSpace(node.ConfigPath)
            ? DefaultServerConfigPath
            : node.ConfigPath.Trim();
    }

    private async Task<IReadOnlyList<TunnelProfile>> LoadClientTunnelsForNodeAsync(
        NodeProfile node,
        CancellationToken cancellationToken)
    {
        var allTunnels = await _tunnelManagementService.ListTunnelsAsync(cancellationToken);
        var tunnels = allTunnels
            .Where(tunnel => string.Equals(tunnel.NodeName, node.Name, StringComparison.OrdinalIgnoreCase))
            .OrderBy(tunnel => tunnel.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        ClientTunnels.Clear();
        foreach (var tunnel in tunnels)
        {
            ClientTunnels.Add(tunnel);
        }

        ClientTunnelCountText = $"共 {ClientTunnels.Count} 条隧道";
        ClientConfigSourceText = ClientTunnels.Count == 0
            ? $"节点 {node.Name} 暂无隧道；请先到【隧道】页新增代理规则。"
            : $"将使用节点 {node.Name} 的服务器地址和 {ClientTunnels.Count} 条隧道生成 frpc.toml。";

        return tunnels;
    }

    private void RefreshTomlPreviewLines()
    {
        TomlPreviewLines.Clear();

        if (string.IsNullOrWhiteSpace(TomlPreview))
        {
            TomlPreviewLines.Add(new TomlPreviewLineViewModel(1, [new TomlPreviewTokenViewModel("TOML 预览将在生成后显示。", TomlPreviewTokenKind.Comment)]));
            return;
        }

        var lines = TomlPreview.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            TomlPreviewLines.Add(new TomlPreviewLineViewModel(index + 1, HighlightTomlLine(lines[index])));
        }
    }

    private static IReadOnlyList<TomlPreviewTokenViewModel> HighlightTomlLine(string line)
    {
        if (string.IsNullOrEmpty(line))
        {
            return [new TomlPreviewTokenViewModel(" ", TomlPreviewTokenKind.Plain)];
        }

        var trimmed = line.TrimStart();
        if (trimmed.StartsWith('#') || trimmed.StartsWith('['))
        {
            return [new TomlPreviewTokenViewModel(line, TomlPreviewTokenKind.Section)];
        }

        var match = TomlAssignmentRegex.Match(line);
        if (!match.Success)
        {
            return [new TomlPreviewTokenViewModel(line, TomlPreviewTokenKind.Plain)];
        }

        var tokens = new List<TomlPreviewTokenViewModel>();
        AddIfNotEmpty(tokens, match.Groups[1].Value, TomlPreviewTokenKind.Plain);
        AddIfNotEmpty(tokens, match.Groups[2].Value, TomlPreviewTokenKind.Key);
        AddIfNotEmpty(tokens, match.Groups[3].Value, TomlPreviewTokenKind.Plain);

        var value = match.Groups[4].Value;
        var lastIndex = 0;
        foreach (Match valueMatch in TomlValueTokenRegex.Matches(value))
        {
            AddIfNotEmpty(tokens, value[lastIndex..valueMatch.Index], TomlPreviewTokenKind.Plain);

            var kind = valueMatch.Value.StartsWith('"')
                ? TomlPreviewTokenKind.String
                : TomlPreviewTokenKind.Number;
            AddIfNotEmpty(tokens, valueMatch.Value, kind);
            lastIndex = valueMatch.Index + valueMatch.Length;
        }

        AddIfNotEmpty(tokens, value[lastIndex..], TomlPreviewTokenKind.Plain);
        return tokens;
    }

    private static void AddIfNotEmpty(ICollection<TomlPreviewTokenViewModel> tokens, string text, TomlPreviewTokenKind kind)
    {
        if (text.Length > 0)
        {
            tokens.Add(new TomlPreviewTokenViewModel(text, kind));
        }
    }
}

public sealed class TomlPreviewLineViewModel(int number, IReadOnlyList<TomlPreviewTokenViewModel> tokens)
{
    public int Number { get; } = number;

    public IReadOnlyList<TomlPreviewTokenViewModel> Tokens { get; } = tokens;
}

public sealed class TomlPreviewTokenViewModel(string text, TomlPreviewTokenKind kind)
{
    public string Text { get; } = text;

    public TomlPreviewTokenKind Kind { get; } = kind;

    public bool IsKey => Kind == TomlPreviewTokenKind.Key;

    public bool IsString => Kind == TomlPreviewTokenKind.String;

    public bool IsNumber => Kind == TomlPreviewTokenKind.Number;

    public bool IsSection => Kind == TomlPreviewTokenKind.Section || Kind == TomlPreviewTokenKind.Comment;
}

public enum TomlPreviewTokenKind
{
    Plain,
    Key,
    String,
    Number,
    Section,
    Comment
}
