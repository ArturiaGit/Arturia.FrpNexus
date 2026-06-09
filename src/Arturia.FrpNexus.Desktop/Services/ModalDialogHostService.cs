using CommunityToolkit.Mvvm.ComponentModel;

namespace Arturia.FrpNexus.Desktop.Services;

public sealed class ModalDialogHostService : ObservableObject, IModalDialogHostService
{
    private readonly object _syncRoot = new();
    private object? _currentDialog;

    public bool IsDialogVisible => CurrentDialog is not null;

    public object? CurrentDialog
    {
        get => _currentDialog;
        private set
        {
            if (SetProperty(ref _currentDialog, value))
            {
                OnPropertyChanged(nameof(IsDialogVisible));
            }
        }
    }

    public void ShowDialog(object dialogViewModel)
    {
        lock (_syncRoot)
        {
            CurrentDialog = dialogViewModel;
        }
    }

    public void CloseDialog(object dialogViewModel)
    {
        lock (_syncRoot)
        {
            if (ReferenceEquals(CurrentDialog, dialogViewModel))
            {
                CurrentDialog = null;
            }
        }
    }
}
