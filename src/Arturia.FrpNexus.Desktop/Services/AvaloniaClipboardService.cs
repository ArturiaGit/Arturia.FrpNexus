using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;

namespace Arturia.FrpNexus.Desktop.Services;

public sealed class AvaloniaClipboardService : IClipboardService
{
    public async Task SetTextAsync(string text, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (Avalonia.Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop
            || desktop.MainWindow?.Clipboard is null)
        {
            throw new InvalidOperationException("当前窗口尚未提供系统剪贴板。");
        }

        await desktop.MainWindow.Clipboard.SetTextAsync(text);
        cancellationToken.ThrowIfCancellationRequested();
    }
}
