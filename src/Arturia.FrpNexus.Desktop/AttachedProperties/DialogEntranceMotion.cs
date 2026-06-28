using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Media;
using Avalonia.Styling;

namespace Arturia.FrpNexus.Desktop.AttachedProperties;

public static class DialogEntranceMotion
{
    public static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<Visual, bool>("IsEnabled", typeof(DialogEntranceMotion));

    static DialogEntranceMotion()
    {
        IsEnabledProperty.Changed.AddClassHandler<Visual>(OnIsEnabledChanged);
        Visual.IsVisibleProperty.Changed.AddClassHandler<Visual>(OnIsVisibleChanged);
    }

    public static bool GetIsEnabled(Visual visual)
    {
        return visual.GetValue(IsEnabledProperty);
    }

    public static void SetIsEnabled(Visual visual, bool value)
    {
        visual.SetValue(IsEnabledProperty, value);
    }

    private static void OnIsEnabledChanged(Visual visual, AvaloniaPropertyChangedEventArgs args)
    {
        if (args.GetNewValue<bool>() && visual.IsVisible)
        {
            _ = RunEnterAnimationAsync(visual);
        }
    }

    private static void OnIsVisibleChanged(Visual visual, AvaloniaPropertyChangedEventArgs args)
    {
        if (GetIsEnabled(visual) && args.GetNewValue<bool>())
        {
            _ = RunEnterAnimationAsync(visual);
        }
    }

    private static async Task RunEnterAnimationAsync(Visual visual)
    {
        var originalTransform = visual.RenderTransform;
        var transform = new ScaleTransform(0.98, 0.98);

        visual.RenderTransformOrigin = RelativePoint.Center;
        visual.RenderTransform = transform;

        var animation = new Animation
        {
            Duration = TimeSpan.FromMilliseconds(140),
            Easing = new SineEaseOut(),
            FillMode = FillMode.Forward,
            Children =
            {
                new KeyFrame
                {
                    Cue = new Cue(0),
                    Setters =
                    {
                        new Setter(ScaleTransform.ScaleXProperty, 0.98d),
                        new Setter(ScaleTransform.ScaleYProperty, 0.98d)
                    }
                },
                new KeyFrame
                {
                    Cue = new Cue(1),
                    Setters =
                    {
                        new Setter(ScaleTransform.ScaleXProperty, 1d),
                        new Setter(ScaleTransform.ScaleYProperty, 1d)
                    }
                }
            }
        };

        try
        {
            await animation.RunAsync(transform);
        }
        finally
        {
            if (visual.RenderTransform == transform)
            {
                visual.RenderTransform = originalTransform;
            }
        }
    }
}
