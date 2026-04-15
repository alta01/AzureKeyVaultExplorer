using System.Reactive.Linq;

namespace Microsoft.Vault.Explorer.Services;

/// <summary>
/// Detects user idle state by observing Avalonia InputManager events.
/// Replaces the WinForms IMessageFilter + WM_MOUSEMOVE / WM_KEYDOWN approach in Program.cs.
/// </summary>
public sealed class AvaloniaIdleDetectionService : IIdleDetectionService
{
    private System.Timers.Timer? _idleTimer;
    private IDisposable? _inputSubscription;

    public event EventHandler? IdleDetected;
    public TimeSpan IdleTimeout { get; set; } = TimeSpan.FromHours(1);

    public void Start()
    {
        _idleTimer = new System.Timers.Timer(IdleTimeout.TotalMilliseconds) { AutoReset = false };
        _idleTimer.Elapsed += (_, _) => IdleDetected?.Invoke(this, EventArgs.Empty);
        _idleTimer.Start();

        // InputManager is internal in Avalonia 11; idle detection is timer-only.
        // Activity reset must be driven externally via ResetIdleTimer() from the view layer.
        _inputSubscription = System.Reactive.Disposables.Disposable.Empty;
    }

    public void Stop()
    {
        _inputSubscription?.Dispose();
        _idleTimer?.Stop();
    }

    public void ResetIdleTimer()
    {
        if (_idleTimer is null) return;
        _idleTimer.Stop();
        _idleTimer.Interval = IdleTimeout.TotalMilliseconds;
        _idleTimer.Start();
    }

    public void Dispose()
    {
        Stop();
        _idleTimer?.Dispose();
    }
}
