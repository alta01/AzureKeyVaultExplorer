using System.Security.Cryptography.X509Certificates;

namespace Microsoft.Vault.Explorer.Services;

/// <summary>
/// Cross-platform abstraction for picking a certificate from the OS certificate store.
/// Windows: native X509Certificate2UI dialog.
/// macOS / Linux: custom Avalonia window backed by X509Store.
/// </summary>
public interface ICertificatePickerService
{
    /// <summary>
    /// Opens a certificate picker for the specified store, filtered to valid (non-expired) certs.
    /// Returns the selected certificate, or null if the user cancelled.
    /// </summary>
    Task<X509Certificate2?> PickCertificateAsync(
        StoreName storeName,
        StoreLocation storeLocation,
        string vaultAlias);
}
