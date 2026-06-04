using Arturia.FrpNexus.Core.Models;

namespace Arturia.FrpNexus.Desktop.ViewModels.Pages;

public sealed record MetricTileViewModel(string Label, string Value, string Icon, FrpNexusStatus Status);
