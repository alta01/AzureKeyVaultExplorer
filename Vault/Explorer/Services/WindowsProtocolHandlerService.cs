using System.Runtime.Versioning;
using Microsoft.Win32;

namespace Microsoft.Vault.Explorer.Services;

/// <summary>
/// Registers vault:// URI scheme via HKCU registry keys.
/// Replaces the ActivationUri.cs registry logic.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class WindowsProtocolHandlerService : IProtocolHandlerService
{
    private const string SchemeKey = @"Software\Classes\vault";
    private const string ExePath = ""; // populated at runtime

    public void Register()
    {
        try
        {
            var exePath = Environment.ProcessPath ?? System.Reflection.Assembly.GetExecutingAssembly().Location;
            using var key = Registry.CurrentUser.CreateSubKey(SchemeKey);
            key.SetValue("", "URL:Vault Protocol");
            key.SetValue("URL Protocol", "");
            using var iconKey = key.CreateSubKey("DefaultIcon");
            iconKey.SetValue("", $"\"{exePath}\",0");
            using var cmdKey = key.CreateSubKey(@"shell\open\command");
            cmdKey.SetValue("", $"\"{exePath}\" \"%1\"");
        }
        catch
        {
            // Non-critical — registration failure should not crash the app.
        }
    }

    public void Unregister()
    {
        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(SchemeKey, throwOnMissingSubKey: false);
        }
        catch { }
    }

    public bool IsRegistered()
    {
        using var key = Registry.CurrentUser.OpenSubKey(SchemeKey);
        return key is not null;
    }
}
