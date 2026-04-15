using System.IO;
using System.Runtime.Versioning;

namespace Microsoft.Vault.Explorer.Services;

/// <summary>
/// Registers vault:// URI scheme via a ~/.local/share/applications/*.desktop file
/// and calls xdg-mime to associate it.
/// </summary>
[SupportedOSPlatform("linux")]
internal sealed class LinuxProtocolHandlerService : IProtocolHandlerService
{
    private static string DesktopFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".local", "share", "applications", "vault-explorer.desktop");

    public void Register()
    {
        try
        {
            var exePath = Environment.ProcessPath ?? System.Reflection.Assembly.GetExecutingAssembly().Location;
            var content = $"""
                [Desktop Entry]
                Name=Vault Explorer
                Exec={exePath} %u
                Type=Application
                MimeType=x-scheme-handler/vault;
                NoDisplay=true
                """;

            Directory.CreateDirectory(Path.GetDirectoryName(DesktopFilePath)!);
            File.WriteAllText(DesktopFilePath, content);

            System.Diagnostics.Process.Start("xdg-mime",
                "default vault-explorer.desktop x-scheme-handler/vault");
        }
        catch { }
    }

    public void Unregister()
    {
        try { File.Delete(DesktopFilePath); } catch { }
    }

    public bool IsRegistered() => File.Exists(DesktopFilePath);
}
