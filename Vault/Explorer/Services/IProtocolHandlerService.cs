namespace Microsoft.Vault.Explorer.Services;

/// <summary>
/// Registers / unregisters the vault:// custom URI scheme on the host OS.
/// Windows: HKCU registry keys.
/// macOS: Info.plist / LSSetDefaultHandlerForURLScheme.
/// Linux: ~/.local/share/applications/*.desktop + xdg-mime.
/// </summary>
public interface IProtocolHandlerService
{
    /// <summary>
    /// Registers the vault:// URI scheme so the OS opens this application when a vault:// link is clicked.
    /// </summary>
    void Register();

    /// <summary>
    /// Removes the vault:// URI scheme registration.
    /// </summary>
    void Unregister();

    /// <summary>
    /// Returns true if the vault:// URI scheme is currently registered to this application.
    /// </summary>
    bool IsRegistered();
}
