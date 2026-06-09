using System;

namespace Arturia.FrpNexus.Desktop.Services;

public sealed class NavigationRequestService : INavigationRequestService
{
    public event EventHandler<string>? NavigationRequested;

    public void RequestNavigation(string pageKey)
    {
        if (!string.IsNullOrWhiteSpace(pageKey))
        {
            NavigationRequested?.Invoke(this, pageKey.Trim());
        }
    }
}
