using System;
using Arturia.FrpNexus.Application.Abstractions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arturia.FrpNexus.Desktop.ViewModels.Dialogs;

public sealed partial class OnboardingDisclaimerViewModel(
    IOnboardingStateService onboardingStateService,
    Action<OnboardingDisclaimerViewModel> close) : ObservableObject
{
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    private bool _hasAcceptedDisclaimer;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _errorText;

    public string CurrentDisclaimerVersion => OnboardingDisclaimer.CurrentVersion;

    public string Title => "首次使用引导与免责声明";

    public string Subtitle => "请先确认 FrpNexus 的适用边界、凭据处理方式和推荐起步流程。";

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorText);

    [RelayCommand(CanExecute = nameof(CanConfirm))]
    private async Task ConfirmAsync()
    {
        ErrorText = null;

        try
        {
            await onboardingStateService.AcceptCurrentDisclaimerAsync(DateTimeOffset.UtcNow);
            close(this);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            ErrorText = "无法保存引导确认状态，请稍后重试。";
        }
    }

    private bool CanConfirm()
    {
        return HasAcceptedDisclaimer;
    }
}
