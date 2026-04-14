namespace Microsoft.Vault.Explorer.Services;

/// <summary>
/// Returns the correct IProtocolHandlerService implementation for the current OS.
/// </summary>
public static class ProtocolHandlerServiceFactory
{
    public static IProtocolHandlerService Create()
    {
        if (OperatingSystem.IsWindows())
            return new WindowsProtocolHandlerService();
        if (OperatingSystem.IsMacOS())
            return new MacProtocolHandlerService();
        return new LinuxProtocolHandlerService();
    }
}
