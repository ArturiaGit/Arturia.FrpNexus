using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Media;
using Avalonia.Styling;

namespace Arturia.FrpNexus.Desktop.Transitions;

public sealed class FadeTranslatePageTransition : IPageTransition
{
    public TimeSpan Duration { get; set; } = TimeSpan.FromMilliseconds(180);

    public double Offset { get; set; } = 8;

    public Easing Easing { get; set; } = new SineEaseOut();

    public async Task Start(Visual? from, Visual? to, bool forward, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        if (from is not null)
        {
            from.Opacity = 0;
        }

        if (to is null)
        {
            return;
        }

        var originalTransform = to.RenderTransform;
        var direction = forward ? 1 : -1;
        var transform = new TranslateTransform(0, Offset * direction);

        to.Opacity = 0;
        to.RenderTransform = transform;

        var animation = new Animation
        {
            Duration = Duration,
            Easing = Easing,
            FillMode = FillMode.Forward,
            Children =
            {
                new KeyFrame
                {
                    Cue = new Cue(0),
                    Setters =
                    {
                        new Setter(Visual.OpacityProperty, 0d),
                        new Setter(TranslateTransform.YProperty, Offset * direction)
                    }
                },
                new KeyFrame
                {
                    Cue = new Cue(1),
                    Setters =
                    {
                        new Setter(Visual.OpacityProperty, 1d),
                        new Setter(TranslateTransform.YProperty, 0d)
                    }
                }
            }
        };

        try
        {
            await animation.RunAsync(to, cancellationToken);
        }
        finally
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                to.Opacity = 1;
                to.RenderTransform = originalTransform;
            }
        }
    }
}
