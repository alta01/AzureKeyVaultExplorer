namespace Microsoft.Vault.Explorer.Services;

/// <summary>
/// Cross-platform abstraction for posting desktop toast notifications.
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Shows a toast notification with the app name as title and <paramref name="body"/> as the message.
    /// Implementations that cannot show a notification must silently no-op.
    /// </summary>
    void ShowToast(string body);
}
