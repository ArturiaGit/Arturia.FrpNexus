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
    private static readonly Regex TomlAssignmentRegex = new(
        @"^(\s*)([A-Za-z0-9_.-]+)(\s*=\s*)(.*)$",
        RegexOptions.Compiled);

    private static readonly Regex TomlValueTokenRegex = new(
        @"""(?:\\""|[^""])*""|\b\d+\b",
        RegexOptions.Compiled);

    private readonly ITomlConfigurationService _tomlConfigurationService;
    private readonly INodeManagementService _nodeManagementService;
    private readonly ITunnelManagementService _tunnelManagementService;
    private readonly IClipboardService _clipboardService;

    [ObservableProperty]
    private NodeProfile? _selectedTargetNode;

    [ObservableProperty]
    private string _tomlPreview = string.Empty;

    [ObservableProperty]
    private string _statusText = "请选择目标节点后查看本地 frpc.toml 预览。";

    [ObservableProperty]
    private string _errorText = string.Empty;

    [ObservableProperty]
    private string _previewActionStatusText = "frpc.toml 预览会根据当前节点的已启用隧道自动生成。";

    [ObservableProperty]
    private string _clientTunnelCountText = "共 0 条隧道";

    [ObservableProperty]
    private string _clientConfigSourceText = "frpc.toml 将由【节点】页的服务器地址和【隧道】页中已启用的代理规则生成。";

    [ObservableProperty]
    private string _targetNodeCountText = "共 0 个目标节点";

    public ConfigurationsPageViewModel(
        ITomlConfigurationService tomlConfigurationService,
        INodeManagementService nodeManagementService,
        ITunnelManagementService tunnelManagementService,
        IClipboardService clipboardService)
        : base("配置预览", "预览当前节点的本地 frpc.toml")
    {
        _tomlConfigurationService = tomlConfigurationService;
        _nodeManagementService = nodeManagementService;
        _tunnelManagementService = tunnelManagementService;
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
            return;
        }
        catch (Exception ex)
        {
            TargetNodeCountText = "目标节点加载失败";
            StatusText = ViewModelErrorText.ForUser("目标节点加载", ex);
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
            var tunnels = await LoadClientTunnelsForNodeAsync(node, cancellationToken);
            GenerateClientPreview(node, tunnels);
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

    partial void OnSelectedTargetNodeChanged(NodeProfile? value)
    {
        TomlPreview = string.Empty;
        ErrorText = string.Empty;
        StatusText = value is null
            ? "请选择目标节点后查看本地 frpc.toml 预览。"
            : $"已选择 {value.Name}，正在生成本地 frpc.toml 预览。";
        _ = RefreshClientTunnelsAsync();
    }

    partial void OnTomlPreviewChanged(string value)
    {
        RefreshTomlPreviewLines();
        PreviewActionStatusText = string.IsNullOrWhiteSpace(value)
            ? "当前没有可预览的 frpc.toml。"
            : "frpc.toml 已更新，可验证语法或复制内容。";
    }

    private async Task<IReadOnlyList<TunnelProfile>> LoadClientTunnelsForNodeAsync(
        NodeProfile node,
        CancellationToken cancellationToken)
    {
        var allTunnels = await _tunnelManagementService.ListTunnelsAsync(cancellationToken);
        var tunnels = allTunnels
            .Where(tunnel => string.Equals(tunnel.NodeName, node.Name, StringComparison.OrdinalIgnoreCase))
            .Where(tunnel => tunnel.Status == FrpNexusStatus.Running)
            .OrderBy(tunnel => tunnel.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        ClientTunnels.Clear();
        foreach (var tunnel in tunnels)
        {
            ClientTunnels.Add(tunnel);
        }

        ClientTunnelCountText = $"共 {ClientTunnels.Count} 条隧道";
        ClientConfigSourceText = ClientTunnels.Count == 0
            ? $"节点 {node.Name} 暂无已启用隧道；请先到【隧道】页启用代理规则。"
            : $"将使用节点 {node.Name} 的服务器地址和 {ClientTunnels.Count} 条已启用隧道生成 frpc.toml 预览。";

        return tunnels;
    }

    private void GenerateClientPreview(NodeProfile node, IReadOnlyList<TunnelProfile> tunnels)
    {
        if (tunnels.Count == 0)
        {
            TomlPreview = string.Empty;
            ErrorText = "当前节点没有已启用隧道，请先到【隧道】页启用代理规则。";
            StatusText = "当前节点没有已启用隧道，暂无可预览的 frpc.toml。";
            return;
        }

        try
        {
            TomlPreview = _tomlConfigurationService.GenerateClientToml(node, tunnels);
            ErrorText = string.Empty;
            StatusText = $"已生成 {node.Name} 的本地 frpc.toml 预览，包含 {tunnels.Count} 条已启用隧道。";
        }
        catch (InvalidOperationException ex)
        {
            TomlPreview = string.Empty;
            ErrorText = ex.Message;
            StatusText = "frpc.toml 预览生成失败。";
        }
        catch (Exception ex)
        {
            TomlPreview = string.Empty;
            ErrorText = ViewModelErrorText.ForUser("frpc.toml 预览", ex);
            StatusText = "frpc.toml 预览生成失败。";
        }
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
