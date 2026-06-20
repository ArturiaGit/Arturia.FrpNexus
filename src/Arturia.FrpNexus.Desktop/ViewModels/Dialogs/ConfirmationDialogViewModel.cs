using System;
using Arturia.FrpNexus.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arturia.FrpNexus.Desktop.ViewModels.Dialogs;

public sealed partial class ConfirmationDialogViewModel(
    string title,
    string message,
    string confirmButtonText,
    string cancelButtonText,
    string severity,
    Action<ConfirmationDialogResult> close,
    string? secondaryButtonText = null) : ObservableObject
{
    public string Title { get; } = title;

    public string Message { get; } = message;

    public string ConfirmButtonText { get; } = confirmButtonText;

    public string CancelButtonText { get; } = cancelButtonText;

    public string? SecondaryButtonText { get; } = string.IsNullOrWhiteSpace(secondaryButtonText)
        ? null
        : secondaryButtonText;

    public bool HasSecondaryAction => !string.IsNullOrWhiteSpace(SecondaryButtonText);

    public string Severity { get; } = string.IsNullOrWhiteSpace(severity) ? "warning" : severity;

    public bool IsWarning => string.Equals(Severity, "warning", StringComparison.OrdinalIgnoreCase);

    public bool IsError => string.Equals(Severity, "error", StringComparison.OrdinalIgnoreCase);

    public bool IsInfo => string.Equals(Severity, "info", StringComparison.OrdinalIgnoreCase);

    [RelayCommand]
    private void Confirm()
    {
        close(ConfirmationDialogResult.Confirm);
    }

    [RelayCommand]
    private void Secondary()
    {
        close(ConfirmationDialogResult.Secondary);
    }

    [RelayCommand]
    private void Cancel()
    {
        close(ConfirmationDialogResult.Cancel);
    }
}
