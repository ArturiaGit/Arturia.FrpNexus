using Arturia.FrpNexus.Application.Abstractions;
using Arturia.FrpNexus.Core.Models;

namespace Arturia.FrpNexus.Infrastructure.Sftp;

public interface ISftpClientAdapter
{
    Task UploadFileAsync(
        NodeProfile node,
        SshCredentialReference credential,
        Stream content,
        string remotePath,
        CancellationToken cancellationToken = default);
}
