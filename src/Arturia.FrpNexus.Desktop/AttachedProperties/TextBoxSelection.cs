using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;

namespace Arturia.FrpNexus.Desktop.AttachedProperties;

public static class TextBoxSelection
{
    private static readonly AttachedProperty<DateTimeOffset> LastClickAtProperty =
        AvaloniaProperty.RegisterAttached<TextBox, DateTimeOffset>(
            "LastClickAt",
            typeof(TextBoxSelection),
            DateTimeOffset.MinValue);

    public static readonly AttachedProperty<bool> SelectAllOnDoubleClickProperty =
        AvaloniaProperty.RegisterAttached<TextBox, bool>(
            "SelectAllOnDoubleClick",
            typeof(TextBoxSelection));

    public static bool GetSelectAllOnDoubleClick(TextBox textBox)
    {
        return textBox.GetValue(SelectAllOnDoubleClickProperty);
    }

    public static void SetSelectAllOnDoubleClick(TextBox textBox, bool value)
    {
        textBox.SetValue(SelectAllOnDoubleClickProperty, value);
    }

    static TextBoxSelection()
    {
        SelectAllOnDoubleClickProperty.Changed.AddClassHandler<TextBox>(OnSelectAllOnDoubleClickChanged);
    }

    private static void OnSelectAllOnDoubleClickChanged(TextBox textBox, AvaloniaPropertyChangedEventArgs args)
    {
        if (args.GetNewValue<bool>())
        {
            textBox.PointerPressed += SelectAllWhenDoubleClicked;
            return;
        }

        textBox.PointerPressed -= SelectAllWhenDoubleClicked;
    }

    private static void SelectAllWhenDoubleClicked(object? sender, PointerPressedEventArgs args)
    {
        if (sender is not TextBox textBox)
        {
            return;
        }

        if (!args.GetCurrentPoint(textBox).Properties.IsLeftButtonPressed)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var lastClickAt = textBox.GetValue(LastClickAtProperty);
        textBox.SetValue(LastClickAtProperty, now);

        if (now - lastClickAt > TimeSpan.FromMilliseconds(450))
        {
            return;
        }

        args.Handled = true;
        Dispatcher.UIThread.Post(() =>
        {
            var textLength = textBox.Text?.Length ?? 0;
            textBox.SelectionStart = 0;
            textBox.SelectionEnd = textLength;
        }, DispatcherPriority.Background);
    }
}
