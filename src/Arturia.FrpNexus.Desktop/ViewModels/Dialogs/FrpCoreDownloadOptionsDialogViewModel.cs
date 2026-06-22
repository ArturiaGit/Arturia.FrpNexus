using System;
using System.Collections.Generic;
using Arturia.FrpNexus.Desktop.Services;
using Arturia.FrpNexus.Desktop.ViewModels.Pages;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arturia.FrpNexus.Desktop.ViewModels.Dialogs;

public sealed partial class FrpCoreDownloadOptionsDialogViewModel(
    Action<FrpCoreDownloadOptions?> close) : ObservableObject
{
    private static readonly SettingsOptionViewModel[] BinaryOptionValues =
    [
        new("frpc", "frpc 客户端"),
        new("frps", "frps 服务端")
    ];

    private static readonly SettingsOptionViewModel[] TargetRuntimeOptionValues =
    [
        new("windows_amd64", "Windows x64"),
        new("linux_amd64", "Linux x64"),
        new("linux_arm64", "Linux ARM64")
    ];

    [ObservableProperty]
    private SettingsOptionViewModel _selectedBinaryOption = BinaryOptionValues[0];

    [ObservableProperty]
    private SettingsOptionViewModel _selectedTargetRuntimeOption = TargetRuntimeOptionValues[0];

    public IReadOnlyList<SettingsOptionViewModel> BinaryOptions => BinaryOptionValues;

    public IReadOnlyList<SettingsOptionViewModel> TargetRuntimeOptions => TargetRuntimeOptionValues;

    [RelayCommand]
    private void Continue()
    {
        close(new FrpCoreDownloadOptions(
            SelectedBinaryOption.Value,
            SelectedTargetRuntimeOption.Value));
    }

    [RelayCommand]
    private void Cancel()
    {
        close(null);
    }
}
