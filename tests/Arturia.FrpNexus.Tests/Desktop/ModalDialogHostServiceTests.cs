using Arturia.FrpNexus.Desktop.Services;

namespace Arturia.FrpNexus.Tests.Desktop;

public sealed class ModalDialogHostServiceTests
{
    [Fact]
    public void InitialState_ShouldBeHidden()
    {
        var service = new ModalDialogHostService();

        Assert.False(service.IsDialogVisible);
        Assert.Null(service.CurrentDialog);
    }

    [Fact]
    public void ShowDialog_ShouldExposeCurrentDialog()
    {
        var service = new ModalDialogHostService();
        var dialog = new object();

        service.ShowDialog(dialog);

        Assert.True(service.IsDialogVisible);
        Assert.Same(dialog, service.CurrentDialog);
    }

    [Fact]
    public void CloseDialog_ShouldHideOnlyMatchingDialog()
    {
        var service = new ModalDialogHostService();
        var dialog = new object();

        service.ShowDialog(dialog);
        service.CloseDialog(dialog);

        Assert.False(service.IsDialogVisible);
        Assert.Null(service.CurrentDialog);
    }

    [Fact]
    public void CloseDialog_ShouldKeepDifferentCurrentDialogVisible()
    {
        var service = new ModalDialogHostService();
        var currentDialog = new object();
        var staleDialog = new object();

        service.ShowDialog(currentDialog);
        service.CloseDialog(staleDialog);

        Assert.True(service.IsDialogVisible);
        Assert.Same(currentDialog, service.CurrentDialog);
    }
}
