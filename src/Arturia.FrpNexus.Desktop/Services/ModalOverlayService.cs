using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Arturia.FrpNexus.Desktop.Services;

public sealed class ModalOverlayService : ObservableObject, IModalOverlayService
{
    private readonly object _syncRoot = new();
    private int _overlayCount;
    private bool _isOverlayVisible;

    public bool IsOverlayVisible
    {
        get => _isOverlayVisible;
        private set => SetProperty(ref _isOverlayVisible, value);
    }

    public IDisposable ShowOverlay()
    {
        lock (_syncRoot)
        {
            _overlayCount++;
            IsOverlayVisible = true;
        }

        return new OverlayScope(this);
    }

    private void HideOverlay()
    {
        lock (_syncRoot)
        {
            if (_overlayCount <= 0)
            {
                IsOverlayVisible = false;
                return;
            }

            _overlayCount--;
            IsOverlayVisible = _overlayCount > 0;
        }
    }

    private sealed class OverlayScope(ModalOverlayService owner) : IDisposable
    {
        private bool _isDisposed;

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            owner.HideOverlay();
        }
    }
}
