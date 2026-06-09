using System.Threading;
using System.Threading.Tasks;
using Arturia.FrpNexus.Core.Models;

namespace Arturia.FrpNexus.Desktop.Services;

public interface INodeConnectionWorkflowDialogService
{
    Task<NodeConnectionWorkflowResult> ShowAsync(
        NodeProfile node,
        NodeConnectionWorkflowOptions? options = null,
        CancellationToken cancellationToken = default);
}

public sealed record NodeConnectionWorkflowOptions(bool SkipInitialDeploymentPresenceCheck = false)
{
    public static NodeConnectionWorkflowOptions Default { get; } = new();

    public static NodeConnectionWorkflowOptions DeployMissingFiles { get; } =
        new(SkipInitialDeploymentPresenceCheck: true);
}

public sealed record NodeConnectionWorkflowResult(
    string NodeName,
    bool IsConnected,
    bool DeploymentReady,
    bool DeploymentChanged,
    bool DeploymentChecked = false);
