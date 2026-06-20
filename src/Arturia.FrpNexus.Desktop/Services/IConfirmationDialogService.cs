using System.Threading;
using System.Threading.Tasks;

namespace Arturia.FrpNexus.Desktop.Services;

public interface IConfirmationDialogService
{
    Task<bool> ShowAsync(
        ConfirmationDialogRequest request,
        CancellationToken cancellationToken = default);

    Task<ConfirmationDialogResult> ShowChoiceAsync(
        ConfirmationDialogChoiceRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ConfirmationDialogRequest(
    string Title,
    string Message,
    string ConfirmButtonText,
    string CancelButtonText,
    string Severity = "warning");

public sealed record ConfirmationDialogChoiceRequest(
    string Title,
    string Message,
    string ConfirmButtonText,
    string SecondaryButtonText,
    string CancelButtonText,
    string Severity = "warning");

public enum ConfirmationDialogResult
{
    Cancel,
    Confirm,
    Secondary
}
