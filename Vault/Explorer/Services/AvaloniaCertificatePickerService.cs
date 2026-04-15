using System.Security.Cryptography.X509Certificates;

namespace Microsoft.Vault.Explorer.Services;

/// <summary>
/// Cross-platform certificate picker backed by X509Store.
/// Phase 1: stub that throws NotSupportedException (drives out the interface).
/// Phase 3: replace with a full Avalonia Window dialog that lists certs.
/// On Windows in Phase 3 we can optionally call X509Certificate2UI
/// via the platform HWND obtained from the parent Avalonia window.
/// </summary>
public sealed class AvaloniaCertificatePickerService : ICertificatePickerService
{
    public Task<X509Certificate2?> PickCertificateAsync(
        StoreName storeName,
        StoreLocation storeLocation,
        string vaultAlias)
    {
        // TODO Phase 3: open an Avalonia Window that lists non-expired certs from the store.
        //
        // var store = new X509Store(storeName, storeLocation);
        // store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);
        // var certs = store.Certificates.Find(X509FindType.FindByTimeValid, DateTime.Now, false);
        // show certs in an Avalonia ListBox, return user selection.

        throw new NotSupportedException(
            "AvaloniaCertificatePickerService is a Phase 3 stub. " +
            "Use WinFormsCertificatePickerService on Windows during Phase 1-3 transition.");
    }
}
