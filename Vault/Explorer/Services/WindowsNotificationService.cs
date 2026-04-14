using System.IO;
using System.Runtime.Versioning;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;
using Microsoft.Vault.Explorer;

namespace Microsoft.Vault.Explorer.Services;

/// <summary>
/// Windows-only toast notification service using WinRT APIs.
/// Used as the Windows implementation until Notification.Avalonia replaces it in Phase 3.
/// </summary>
[SupportedOSPlatform("windows10.0.17763.0")]
public sealed class WindowsNotificationService : INotificationService
{
    public void ShowToast(string body)
    {
        try
        {
            var toastXml = ToastNotificationManager.GetTemplateContent(ToastTemplateType.ToastImageAndText02);

            var stringElements = toastXml.GetElementsByTagName("text");
            stringElements[0].AppendChild(toastXml.CreateTextNode(Globals.AppName));
            stringElements[1].AppendChild(toastXml.CreateTextNode(body));

            var imagePath = "file:///" + Path.ChangeExtension(
                Environment.ProcessPath ?? System.Reflection.Assembly.GetExecutingAssembly().Location,
                ".png");
            var imageElements = toastXml.GetElementsByTagName("image");
            imageElements[0].Attributes.GetNamedItem("src").NodeValue = imagePath;

            var toast = new ToastNotification(toastXml);
            ToastNotificationManager.CreateToastNotifier(Globals.AppName).Show(toast);
        }
        catch
        {
            // Non-critical — never crash the app over a toast notification.
        }
    }
}
