using System;
using System.ComponentModel;

namespace Arturia.FrpNexus.Desktop.Services;

public interface IModalOverlayService : INotifyPropertyChanged
{
    bool IsOverlayVisible { get; }

    IDisposable ShowOverlay();
}
