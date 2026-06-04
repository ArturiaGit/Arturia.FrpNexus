using Arturia.FrpNexus.Core.Models;

namespace Arturia.FrpNexus.Desktop.Models;

public sealed record StatusBadge(string Text, FrpNexusStatus Status);
