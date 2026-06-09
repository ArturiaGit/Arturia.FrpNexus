using System.Runtime.Serialization;
using Arturia.FrpNexus.Desktop.AttachedProperties;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace Arturia.FrpNexus.Tests.Desktop;

public sealed class DismissOnOutsidePointerPressedTests
{
    [Fact]
    public void ShouldDismissForPointerSource_ShouldIgnoreTargetContent()
    {
        var target = new Border();
        var child = new TextBlock();
        target.Child = child;

        var shouldDismiss = DismissOnOutsidePointerPressed.ShouldDismissForPointerSource(child, target);

        Assert.False(shouldDismiss);
    }

    [Fact]
    public void ShouldDismissForPointerSource_ShouldDismissOrdinaryOutsideContent()
    {
        var target = new Border();
        var outside = new TextBlock();

        var shouldDismiss = DismissOnOutsidePointerPressed.ShouldDismissForPointerSource(outside, target);

        Assert.True(shouldDismiss);
    }

    [Fact]
    public void ShouldDismissForPointerSource_ShouldIgnoreDismissIgnoredContent()
    {
        var target = new Border();
        var ignored = new Border();
        var child = new TextBlock();
        ignored.Child = child;
        DismissOnOutsidePointerPressed.SetIsDismissIgnored(ignored, true);

        var shouldDismiss = DismissOnOutsidePointerPressed.ShouldDismissForPointerSource(child, target);

        Assert.False(shouldDismiss);
    }

    [Fact]
    public void ShouldDismissForPointerSource_ShouldIgnorePopupRootContent()
    {
        var target = new Border();
#pragma warning disable SYSLIB0050
        var popupRoot = (PopupRoot)FormatterServices.GetUninitializedObject(typeof(PopupRoot));
#pragma warning restore SYSLIB0050

        var shouldDismiss = DismissOnOutsidePointerPressed.ShouldDismissForPointerSource(popupRoot, target);

        Assert.False(shouldDismiss);
    }
}
