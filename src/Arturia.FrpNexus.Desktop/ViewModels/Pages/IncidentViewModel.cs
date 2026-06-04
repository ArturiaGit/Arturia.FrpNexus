using Arturia.FrpNexus.Core.Models;

namespace Arturia.FrpNexus.Desktop.ViewModels.Pages;

public sealed record IncidentViewModel(string Title, string Time, string Message, FrpNexusStatus Status);
