using Avalonia;
using Avalonia.Controls;

namespace Arturia.FrpNexus.Desktop.Controls;

public partial class KeyValueRowControl : UserControl
{
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<KeyValueRowControl, string>(nameof(Label), string.Empty);

    public static readonly StyledProperty<string> ValueProperty =
        AvaloniaProperty.Register<KeyValueRowControl, string>(nameof(Value), string.Empty);

    public static readonly StyledProperty<bool> IsCodeProperty =
        AvaloniaProperty.Register<KeyValueRowControl, bool>(nameof(IsCode));

    public KeyValueRowControl()
    {
        InitializeComponent();
    }

    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public bool IsCode
    {
        get => GetValue(IsCodeProperty);
        set => SetValue(IsCodeProperty, value);
    }
}
