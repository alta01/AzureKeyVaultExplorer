using Avalonia.Controls;

namespace Microsoft.Vault.Explorer.Services;

/// <summary>
/// Avalonia dialog service.
/// Phase 1: stub that uses simple message boxes.
/// Phase 3: replace stubs with full AXAML-based dialog windows (ExceptionDialogView, etc.).
/// </summary>
public sealed class AvaloniaDialogService : IDialogService
{
    private Window? GetMainWindow() =>
        (Application.Current?.ApplicationLifetime
            as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)
            ?.MainWindow;

    public Task ShowMessageAsync(string title, string message)
    {
        // TODO Phase 3: open MessageDialogView.axaml
        return Task.CompletedTask;
    }

    public Task ShowErrorAsync(string title, string message, Exception? ex = null)
    {
        // TODO Phase 3: open ExceptionDialogView.axaml
        return Task.CompletedTask;
    }

    public Task<bool> ShowConfirmAsync(string title, string message)
    {
        // TODO Phase 3: open ConfirmDialogView.axaml
        return Task.FromResult(true);
    }

    public Task<bool> ShowAutoClosingConfirmAsync(string title, string message, TimeSpan timeout)
    {
        // TODO Phase 3: open a timed confirmation dialog with DispatcherTimer countdown.
        return Task.FromResult(true);
    }
}
