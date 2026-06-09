using System.ComponentModel;

namespace Arturia.FrpNexus.Desktop.Services;

public interface IModalDialogHostService : INotifyPropertyChanged
{
    bool IsDialogVisible { get; }

    object? CurrentDialog { get; }

    void ShowDialog(object dialogViewModel);

    void CloseDialog(object dialogViewModel);
}
