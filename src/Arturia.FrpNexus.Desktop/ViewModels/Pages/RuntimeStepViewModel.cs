using Arturia.FrpNexus.Core.Models;

namespace Arturia.FrpNexus.Desktop.ViewModels.Pages;

public sealed record RuntimeStepViewModel(string Title, string Description, FrpNexusStatus Status);
