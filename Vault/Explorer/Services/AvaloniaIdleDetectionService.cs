using System.Reactive.Linq;
using Avalonia.Input;

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

        // Subscribe to ALL input events and reset the idle timer on each one.
        _inputSubscription = Observable
            .FromEventPattern<RawInputEventArgs>(
                h => InputManager.Current.Process += h,
                h => InputManager.Current.Process -= h)
            .Subscribe(_ => ResetIdleTimer());
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
