namespace Microsoft.Vault.Explorer.Services;

/// <summary>
/// Cross-platform abstraction for showing modal dialogs.
/// Full implementation is provided in Phase 3 (AvaloniaDialogService).
/// </summary>
public interface IDialogService
{
    /// <summary>Shows an informational message box. Returns when the user dismisses it.</summary>
    Task ShowMessageAsync(string title, string message);

    /// <summary>Shows an error message box.</summary>
    Task ShowErrorAsync(string title, string message, Exception? ex = null);

    /// <summary>Shows a yes/no confirmation dialog. Returns true if the user confirmed.</summary>
    Task<bool> ShowConfirmAsync(string title, string message);

    /// <summary>
    /// Shows a message box that auto-closes after <paramref name="timeout"/>.
    /// Returns true if the user confirmed before timeout, false if it timed out.
    /// </summary>
    Task<bool> ShowAutoClosingConfirmAsync(string title, string message, TimeSpan timeout);
}
