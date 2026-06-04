using Avalonia;
using Avalonia.Controls;

namespace Arturia.FrpNexus.Desktop.Controls;

public partial class SettingsRowControl : UserControl
{
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<SettingsRowControl, string>(nameof(Title), string.Empty);

    public static readonly StyledProperty<string> DescriptionProperty =
        AvaloniaProperty.Register<SettingsRowControl, string>(nameof(Description), string.Empty);

    public static readonly StyledProperty<object?> ValueContentProperty =
        AvaloniaProperty.Register<SettingsRowControl, object?>(nameof(ValueContent));

    public SettingsRowControl()
    {
        InitializeComponent();
    }

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Description
    {
        get => GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public object? ValueContent
    {
        get => GetValue(ValueContentProperty);
        set => SetValue(ValueContentProperty, value);
    }
}
