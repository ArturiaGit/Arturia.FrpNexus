using Arturia.FrpNexus.Desktop.ViewModels.Pages;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Arturia.FrpNexus.Desktop.Models;

public sealed partial class NavigationItem : ObservableObject
{
    [ObservableProperty]
    private bool _isSelected;

    public NavigationItem(string title, string icon, PageViewModel page, ICommand navigateCommand)
    {
        Title = title;
        Icon = icon;
        Page = page;
        NavigateCommand = navigateCommand;
    }

    public string Title { get; }

    public string Icon { get; }

    public PageViewModel Page { get; }

    public ICommand NavigateCommand { get; }
}
