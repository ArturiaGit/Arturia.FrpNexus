namespace Arturia.FrpNexus.Desktop.ViewModels.Pages;

public sealed class SettingPreviewViewModel : ViewModelBase
{
    private string _value;

    public SettingPreviewViewModel(string label, string value)
    {
        Label = label;
        _value = value;
    }

    public string Label { get; }

    public string Value
    {
        get => _value;
        set => SetProperty(ref _value, value);
    }
}
