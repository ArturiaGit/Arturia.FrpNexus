namespace Arturia.FrpNexus.Desktop.ViewModels;

public sealed class NavigationItemViewModel
{
    public NavigationItemViewModel(string icon, string title, PageViewModel page)
    {
        Icon = icon;
        Title = title;
        Page = page;
    }

    public string Icon { get; }

    public string Title { get; }

    public PageViewModel Page { get; }
}
