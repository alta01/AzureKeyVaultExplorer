using System.Diagnostics;

namespace Microsoft.Vault.Explorer.Services;

/// <summary>
/// Cross-platform toast notification service.
/// Phase 1: logs to Debug output as a stub.
/// Phase 3+: wire to Notification.Avalonia NuGet package for native OS notifications.
/// </summary>
public sealed class AvaloniaNotificationService : INotificationService
{
    public void ShowToast(string body)
    {
        // TODO Phase 3: replace with Notification.Avalonia NuGet call.
        // Example:
        //   var manager = NotificationManagerWrapper.Create();
        //   await manager.ShowNotificationAsync(Globals.AppName, body);
        Debug.WriteLine($"[Notification] {body}");
    }
}
