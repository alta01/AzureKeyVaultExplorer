namespace Microsoft.Vault.Explorer.Services;

/// <summary>
/// Detects user idle state by monitoring input events.
/// Replaces the WinForms IMessageFilter + WM_* message approach used in Program.cs.
/// Avalonia implementation subscribes to InputManager.Current.Process.
/// </summary>
public interface IIdleDetectionService : IDisposable
{
    /// <summary>
    /// Fired when the user has been idle for longer than <see cref="IdleTimeout"/>.
    /// </summary>
    event EventHandler IdleDetected;

    /// <summary>The duration of inactivity before <see cref="IdleDetected"/> fires.</summary>
    TimeSpan IdleTimeout { get; set; }

    /// <summary>Resets the idle timer (call on any user input).</summary>
    void ResetIdleTimer();

    /// <summary>Starts monitoring for idle.</summary>
    void Start();

    /// <summary>Stops monitoring.</summary>
    void Stop();
}
