using Avalonia.Controls;
using Arturia.FrpNexus.Desktop.ViewModels;

namespace Arturia.FrpNexus.Desktop.Views;

public partial class MainWindow : Window
{
    private bool _isCloseConfirmed;
    private bool _isCloseConfirmationRunning;

    public MainWindow()
    {
        InitializeComponent();
    }

    protected override async void OnClosing(WindowClosingEventArgs e)
    {
        if (_isCloseConfirmed || DataContext is not MainWindowViewModel viewModel)
        {
            base.OnClosing(e);
            return;
        }

        e.Cancel = true;
        if (_isCloseConfirmationRunning)
        {
            return;
        }

        _isCloseConfirmationRunning = true;
        try
        {
            if (await viewModel.ConfirmCloseAsync())
            {
                _isCloseConfirmed = true;
                Close();
            }
        }
        finally
        {
            _isCloseConfirmationRunning = false;
        }
    }
}
