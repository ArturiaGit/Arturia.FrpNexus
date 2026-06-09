using Arturia.FrpNexus.Desktop.Services;

namespace Arturia.FrpNexus.Tests.Desktop;

public sealed class ModalOverlayServiceTests
{
    [Fact]
    public void InitialState_ShouldBeHidden()
    {
        var service = new ModalOverlayService();

        Assert.False(service.IsOverlayVisible);
    }

    [Fact]
    public void ShowOverlay_ShouldStayVisibleUntilScopeIsDisposed()
    {
        var service = new ModalOverlayService();

        using var overlay = service.ShowOverlay();

        Assert.True(service.IsOverlayVisible);

        overlay.Dispose();

        Assert.False(service.IsOverlayVisible);
    }

    [Fact]
    public void ShowOverlay_ShouldSupportNestedScopes()
    {
        var service = new ModalOverlayService();

        using var outer = service.ShowOverlay();
        using var inner = service.ShowOverlay();

        Assert.True(service.IsOverlayVisible);

        inner.Dispose();

        Assert.True(service.IsOverlayVisible);

        outer.Dispose();

        Assert.False(service.IsOverlayVisible);
    }

    [Fact]
    public void OverlayScopeDispose_ShouldBeIdempotent()
    {
        var service = new ModalOverlayService();
        var overlay = service.ShowOverlay();

        overlay.Dispose();
        overlay.Dispose();

        Assert.False(service.IsOverlayVisible);
    }
}
