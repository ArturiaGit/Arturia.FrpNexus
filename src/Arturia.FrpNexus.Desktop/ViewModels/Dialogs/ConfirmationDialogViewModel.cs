using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arturia.FrpNexus.Desktop.ViewModels.Dialogs;

public sealed partial class ConfirmationDialogViewModel(
    string title,
    string message,
    string confirmButtonText,
    string cancelButtonText,
    string severity,
    Action<bool> close) : ObservableObject
{
    public string Title { get; } = title;

    public string Message { get; } = message;

    public string ConfirmButtonText { get; } = confirmButtonText;

    public string CancelButtonText { get; } = cancelButtonText;

    public string Severity { get; } = string.IsNullOrWhiteSpace(severity) ? "warning" : severity;

    public bool IsWarning => string.Equals(Severity, "warning", StringComparison.OrdinalIgnoreCase);

    public bool IsError => string.Equals(Severity, "error", StringComparison.OrdinalIgnoreCase);

    public bool IsInfo => string.Equals(Severity, "info", StringComparison.OrdinalIgnoreCase);

    [RelayCommand]
    private void Confirm()
    {
        close(true);
    }

    [RelayCommand]
    private void Cancel()
    {
        close(false);
    }
}
