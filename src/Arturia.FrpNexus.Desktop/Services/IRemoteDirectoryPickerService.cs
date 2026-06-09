using System.Threading;
using System.Threading.Tasks;
using Arturia.FrpNexus.Application.Abstractions;
using Arturia.FrpNexus.Core.Models;

namespace Arturia.FrpNexus.Desktop.Services;

public interface IRemoteDirectoryPickerService
{
    Task<string?> PickRemoteDirectoryAsync(
        NodeProfile node,
        SshCredentialReference credential,
        string initialDirectory,
        CancellationToken cancellationToken = default);
}
