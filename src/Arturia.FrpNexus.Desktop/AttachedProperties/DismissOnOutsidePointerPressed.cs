using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace Arturia.FrpNexus.Desktop.AttachedProperties;

public static class DismissOnOutsidePointerPressed
{
    public static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<Control, bool>(
            "IsEnabled",
            typeof(DismissOnOutsidePointerPressed));

    public static readonly AttachedProperty<Control?> DismissTargetProperty =
        AvaloniaProperty.RegisterAttached<Control, Control?>(
            "DismissTarget",
            typeof(DismissOnOutsidePointerPressed));

    public static readonly AttachedProperty<ICommand?> DismissCommandProperty =
        AvaloniaProperty.RegisterAttached<Control, ICommand?>(
            "DismissCommand",
            typeof(DismissOnOutsidePointerPressed));

    public static readonly AttachedProperty<bool> IsDismissIgnoredProperty =
        AvaloniaProperty.RegisterAttached<Control, bool>(
            "IsDismissIgnored",
            typeof(DismissOnOutsidePointerPressed));

    public static bool GetIsEnabled(Control control)
    {
        return control.GetValue(IsEnabledProperty);
    }

    public static void SetIsEnabled(Control control, bool value)
    {
        control.SetValue(IsEnabledProperty, value);
    }

    public static Control? GetDismissTarget(Control control)
    {
        return control.GetValue(DismissTargetProperty);
    }

    public static void SetDismissTarget(Control control, Control? value)
    {
        control.SetValue(DismissTargetProperty, value);
    }

    public static ICommand? GetDismissCommand(Control control)
    {
        return control.GetValue(DismissCommandProperty);
    }

    public static void SetDismissCommand(Control control, ICommand? value)
    {
        control.SetValue(DismissCommandProperty, value);
    }

    public static bool GetIsDismissIgnored(Control control)
    {
        return control.GetValue(IsDismissIgnoredProperty);
    }

    public static void SetIsDismissIgnored(Control control, bool value)
    {
        control.SetValue(IsDismissIgnoredProperty, value);
    }

    static DismissOnOutsidePointerPressed()
    {
        IsEnabledProperty.Changed.AddClassHandler<Control>(OnIsEnabledChanged);
    }

    private static void OnIsEnabledChanged(Control host, AvaloniaPropertyChangedEventArgs args)
    {
        host.RemoveHandler(InputElement.PointerPressedEvent, OnHostPointerPressed);

        if (args.GetNewValue<bool>())
        {
            host.AddHandler(
                InputElement.PointerPressedEvent,
                OnHostPointerPressed,
                RoutingStrategies.Tunnel,
                handledEventsToo: true);
        }
    }

    private static void OnHostPointerPressed(object? sender, PointerPressedEventArgs args)
    {
        if (sender is not Control host)
        {
            return;
        }

        var target = GetDismissTarget(host);
        var command = GetDismissCommand(host);
        if (target is null || command is null || !target.IsVisible)
        {
            return;
        }

        if (!ShouldDismissForPointerSource(args.Source, target))
        {
            return;
        }

        if (command.CanExecute(null))
        {
            command.Execute(null);
        }
    }

    internal static bool ShouldDismissForPointerSource(object? source, Control target)
    {
        if (IsPointerSourceInsideTarget(source, target))
        {
            return false;
        }

        if (IsPointerSourceInsidePopup(source))
        {
            return false;
        }

        if (IsPointerSourceDismissIgnored(source))
        {
            return false;
        }

        return true;
    }

    private static bool IsPointerSourceInsideTarget(object? source, Control target)
    {
        if (ReferenceEquals(source, target))
        {
            return true;
        }

        if (source is not Visual visual)
        {
            return false;
        }

        for (var current = visual; current is not null; current = current.GetVisualParent())
        {
            if (ReferenceEquals(current, target))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsPointerSourceInsidePopup(object? source)
    {
        if (source is not Visual visual)
        {
            return false;
        }

        for (var current = visual; current is not null; current = current.GetVisualParent())
        {
            if (current is PopupRoot or OverlayPopupHost)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsPointerSourceDismissIgnored(object? source)
    {
        if (source is not Visual visual)
        {
            return false;
        }

        for (var current = visual; current is not null; current = current.GetVisualParent())
        {
            if (current is Control control && GetIsDismissIgnored(control))
            {
                return true;
            }
        }

        return false;
    }
}
