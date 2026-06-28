using System;
using System.Threading;
using System.Threading.Tasks;
using Arturia.FrpNexus.Application.Abstractions;
using Arturia.FrpNexus.Desktop.ViewModels.Dialogs;

namespace Arturia.FrpNexus.Desktop.Services;

public sealed class OnboardingDialogService(
    IOnboardingStateService onboardingStateService,
    IModalOverlayService modalOverlayService,
    IModalDialogHostService modalDialogHostService) : IOnboardingDialogService
{
    private IDisposable? _overlayScope;

    public async Task ShowIfRequiredAsync(CancellationToken cancellationToken = default)
    {
        var onboardingState = await onboardingStateService.GetStateAsync(cancellationToken);
        if (onboardingState.IsCurrentDisclaimerAccepted)
        {
            return;
        }

        ShowDialog();
    }

    private void ShowDialog()
    {
        _overlayScope?.Dispose();
        _overlayScope = modalOverlayService.ShowOverlay();

        var viewModel = new OnboardingDisclaimerViewModel(
            onboardingStateService,
            CloseDialog);
        modalDialogHostService.ShowDialog(viewModel);
    }

    private void CloseDialog(OnboardingDisclaimerViewModel viewModel)
    {
        modalDialogHostService.CloseDialog(viewModel);
        _overlayScope?.Dispose();
        _overlayScope = null;
    }
}
