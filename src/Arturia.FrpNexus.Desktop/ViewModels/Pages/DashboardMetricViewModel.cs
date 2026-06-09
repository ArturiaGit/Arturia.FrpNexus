namespace Arturia.FrpNexus.Desktop.ViewModels.Pages;

public sealed class DashboardMetricViewModel
{
    public DashboardMetricViewModel(string label, string value, string hint)
    {
        Label = label;
        Value = value;
        Hint = hint;
    }

    public string Label { get; }

    public string Value { get; }

    public string Hint { get; }
}
