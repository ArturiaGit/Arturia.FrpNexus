using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arturia.FrpNexus.Desktop.ViewModels.Pages;

public sealed partial class CredentialSecurityNodeViewModel : ObservableObject
{
    private readonly Func<CredentialSecurityNodeViewModel, CancellationToken, Task> _clearSavedSessionPasswordAsync;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ClearSavedSessionPasswordCommand))]
    private bool _hasSavedSessionPassword;

    public CredentialSecurityNodeViewModel(
        string nodeName,
        string authenticationModeText,
        string privateKeySummary,
        bool hasSavedSessionPassword,
        Func<CredentialSecurityNodeViewModel, CancellationToken, Task> clearSavedSessionPasswordAsync)
    {
        NodeName = nodeName;
        AuthenticationModeText = authenticationModeText;
        PrivateKeySummary = privateKeySummary;
        HasSavedSessionPassword = hasSavedSessionPassword;
        _clearSavedSessionPasswordAsync = clearSavedSessionPasswordAsync;
    }

    public string NodeName { get; }

    public string AuthenticationModeText { get; }

    public string PrivateKeySummary { get; }

    public string SavedPasswordStatusText => HasSavedSessionPassword ? "已保存" : "未保存";

    public bool CanClearSavedSessionPassword => HasSavedSessionPassword;

    partial void OnHasSavedSessionPasswordChanged(bool value)
    {
        OnPropertyChanged(nameof(SavedPasswordStatusText));
        OnPropertyChanged(nameof(CanClearSavedSessionPassword));
    }

    [RelayCommand(CanExecute = nameof(CanClearSavedSessionPassword))]
    private Task ClearSavedSessionPasswordAsync(CancellationToken cancellationToken = default)
    {
        return _clearSavedSessionPasswordAsync(this, cancellationToken);
    }
}
