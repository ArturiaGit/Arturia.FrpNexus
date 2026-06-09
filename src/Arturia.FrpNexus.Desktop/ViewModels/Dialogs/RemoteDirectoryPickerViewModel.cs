using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Arturia.FrpNexus.Application.Abstractions;
using Arturia.FrpNexus.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arturia.FrpNexus.Desktop.ViewModels.Dialogs;

public sealed partial class RemoteDirectoryPickerViewModel : ViewModelBase
{
    private readonly IRemoteDirectoryService _remoteDirectoryService;
    private readonly NodeProfile _node;
    private readonly SshCredentialReference _credential;
    private readonly Action<string?> _close;

    [ObservableProperty]
    private string _currentPath;

    [ObservableProperty]
    private string _newDirectoryName = string.Empty;

    [ObservableProperty]
    private string _statusTitle = "远程目录";

    [ObservableProperty]
    private string _statusText = "正在读取远程目录...";

    [ObservableProperty]
    private string _statusSeverity = "info";

    [ObservableProperty]
    private bool _isBusy;

    public RemoteDirectoryPickerViewModel(
        IRemoteDirectoryService remoteDirectoryService,
        NodeProfile node,
        SshCredentialReference credential,
        string initialDirectory,
        Action<string?> close)
    {
        _remoteDirectoryService = remoteDirectoryService;
        _node = node;
        _credential = credential;
        _close = close;
        CurrentPath = NormalizeDirectoryPath(initialDirectory);
        Directories = [];
    }

    public ObservableCollection<RemoteDirectoryEntryViewModel> Directories { get; }

    public bool HasDirectories => Directories.Count > 0;

    public bool CanNavigateUp => !string.Equals(CurrentPath, "/", StringComparison.Ordinal);

    public bool CanCreateDirectory => !IsBusy && !string.IsNullOrWhiteSpace(NewDirectoryName);

    public bool IsStatusInfo => string.Equals(StatusSeverity, "info", StringComparison.OrdinalIgnoreCase);

    public bool IsStatusSuccess => string.Equals(StatusSeverity, "success", StringComparison.OrdinalIgnoreCase);

    public bool IsStatusWarning => string.Equals(StatusSeverity, "warning", StringComparison.OrdinalIgnoreCase);

    public bool IsStatusError => string.Equals(StatusSeverity, "error", StringComparison.OrdinalIgnoreCase);

    [RelayCommand]
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        IsBusy = true;
        SetStatus("正在读取远程目录", CurrentPath, "info");

        try
        {
            var result = await _remoteDirectoryService.ListDirectoriesAsync(
                new RemoteDirectoryListRequest(_node, _credential, CurrentPath),
                cancellationToken);

            Directories.Clear();
            foreach (var directory in result.Directories)
            {
                Directories.Add(new RemoteDirectoryEntryViewModel(directory));
            }

            OnPropertyChanged(nameof(HasDirectories));
            SetStatus(
                result.Status == FrpNexusStatus.Error ? "读取失败" : "读取成功",
                result.Status == FrpNexusStatus.Error ? result.Message : $"当前目录：{result.RemotePath}",
                result.Status == FrpNexusStatus.Error ? "error" : "success");
        }
        catch (OperationCanceledException)
        {
            SetStatus("读取已取消", "远程目录读取已取消。", "warning");
        }
        catch (Exception ex)
        {
            SetStatus("读取失败", ViewModelErrorText.ForUser("远程目录读取", ex), "error");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task OpenDirectoryAsync(RemoteDirectoryEntryViewModel? directory)
    {
        if (directory is null || IsBusy)
        {
            return;
        }

        CurrentPath = directory.FullPath;
        await LoadAsync();
    }

    [RelayCommand]
    private async Task NavigateUpAsync()
    {
        if (!CanNavigateUp || IsBusy)
        {
            return;
        }

        CurrentPath = GetParentDirectory(CurrentPath);
        await LoadAsync();
    }

    [RelayCommand]
    private async Task CreateDirectoryAsync(CancellationToken cancellationToken = default)
    {
        var directoryName = NewDirectoryName.Trim();
        if (string.IsNullOrWhiteSpace(directoryName))
        {
            SetStatus("无法创建目录", "请输入目录名称。", "warning");
            return;
        }

        if (directoryName.Contains('/', StringComparison.Ordinal)
            || directoryName.Contains('\\', StringComparison.Ordinal)
            || directoryName.Contains('\0', StringComparison.Ordinal))
        {
            SetStatus("目录名称无效", "目录名称不能包含路径分隔符。", "warning");
            return;
        }

        IsBusy = true;
        var targetPath = CombineRemotePath(CurrentPath, directoryName);
        SetStatus("正在创建目录", targetPath, "info");

        try
        {
            var result = await _remoteDirectoryService.CreateDirectoryAsync(
                new RemoteDirectoryCreateRequest(_node, _credential, targetPath),
                cancellationToken);

            if (result.Status == FrpNexusStatus.Error)
            {
                SetStatus("创建失败", result.Message, "error");
                return;
            }

            NewDirectoryName = string.Empty;
            SetStatus("创建成功", $"已创建：{targetPath}", "success");
            await LoadAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            SetStatus("创建已取消", "远程目录创建已取消。", "warning");
        }
        catch (Exception ex)
        {
            SetStatus("创建失败", ViewModelErrorText.ForUser("远程目录创建", ex), "error");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void SelectCurrentDirectory()
    {
        _close(CurrentPath);
    }

    [RelayCommand]
    private void Cancel()
    {
        _close(null);
    }

    partial void OnCurrentPathChanged(string value)
    {
        OnPropertyChanged(nameof(CanNavigateUp));
    }

    partial void OnNewDirectoryNameChanged(string value)
    {
        OnPropertyChanged(nameof(CanCreateDirectory));
    }

    partial void OnStatusSeverityChanged(string value)
    {
        OnPropertyChanged(nameof(IsStatusInfo));
        OnPropertyChanged(nameof(IsStatusSuccess));
        OnPropertyChanged(nameof(IsStatusWarning));
        OnPropertyChanged(nameof(IsStatusError));
    }

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(CanCreateDirectory));
        OnPropertyChanged(nameof(CanNavigateUp));
    }

    private void SetStatus(string title, string message, string severity)
    {
        StatusTitle = title;
        StatusText = message;
        StatusSeverity = severity;
    }

    private static string NormalizeDirectoryPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "/";
        }

        var normalized = path.Trim().Replace('\\', '/');
        while (normalized.Contains("//", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("//", "/", StringComparison.Ordinal);
        }

        if (!normalized.StartsWith("/", StringComparison.Ordinal))
        {
            normalized = "/" + normalized;
        }

        return normalized.Length > 1 ? normalized.TrimEnd('/') : normalized;
    }

    private static string GetParentDirectory(string path)
    {
        var normalized = NormalizeDirectoryPath(path);
        var lastSlash = normalized.LastIndexOf('/');
        return lastSlash <= 0 ? "/" : normalized[..lastSlash];
    }

    private static string CombineRemotePath(string parent, string child)
    {
        return string.Equals(parent, "/", StringComparison.Ordinal)
            ? $"/{child}"
            : $"{parent.TrimEnd('/')}/{child}";
    }
}

public sealed class RemoteDirectoryEntryViewModel(RemoteDirectoryEntry entry)
{
    public string Name => entry.Name;

    public string FullPath => entry.FullPath;
}
