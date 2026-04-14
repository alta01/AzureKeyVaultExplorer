using System.Runtime.Versioning;

namespace Microsoft.Vault.Explorer.Services;

/// <summary>
/// On macOS the vault:// scheme is registered via the app's Info.plist CFBundleURLTypes.
/// At runtime this service is a no-op — the scheme is pre-registered in the .app bundle.
/// Dynamic registration would require LSSetDefaultHandlerForURLScheme (private API).
/// </summary>
[SupportedOSPlatform("macos")]
internal sealed class MacProtocolHandlerService : IProtocolHandlerService
{
    public void Register()
    {
        // No-op: vault:// is registered via Info.plist in the .app bundle.
        // See: https://developer.apple.com/documentation/bundleresources/information_property_list/cfbundleurltypes
    }

    public void Unregister()
    {
        // No-op on macOS.
    }

    public bool IsRegistered()
    {
        // Assume registered if running from a .app bundle (Info.plist is present).
        return true;
    }
}
