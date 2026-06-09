namespace Arturia.FrpNexus.Desktop.ViewModels;

public abstract class PageViewModel : ViewModelBase
{
    protected PageViewModel(string title, string subtitle)
    {
        Title = title;
        Subtitle = subtitle;
    }

    public string Title { get; }

    public string Subtitle { get; }
}
