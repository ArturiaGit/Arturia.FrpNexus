using System;

namespace Arturia.FrpNexus.Desktop.Services;

public interface INavigationRequestService
{
    event EventHandler<string>? NavigationRequested;

    void RequestNavigation(string pageKey);
}
