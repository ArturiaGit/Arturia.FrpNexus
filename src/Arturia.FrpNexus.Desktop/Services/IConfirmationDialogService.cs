using System.Threading;
using System.Threading.Tasks;

namespace Arturia.FrpNexus.Desktop.Services;

public interface IConfirmationDialogService
{
    Task<bool> ShowAsync(
        ConfirmationDialogRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ConfirmationDialogRequest(
    string Title,
    string Message,
    string ConfirmButtonText,
    string CancelButtonText,
    string Severity = "warning");
