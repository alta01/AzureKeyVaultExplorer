using System.Runtime.Versioning;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Vault.Explorer;

namespace Microsoft.Vault.Explorer.Services;

/// <summary>
/// Windows-only certificate picker using the native X509Certificate2UI dialog.
/// Bridge service for Phase 1-3 while WinForms is still active.
/// Deleted in Phase 5 when WinForms is removed.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WinFormsCertificatePickerService : ICertificatePickerService
{
    public Task<X509Certificate2?> PickCertificateAsync(
        StoreName storeName,
        StoreLocation storeLocation,
        string vaultAlias)
    {
        using var store = new X509Store(storeName, storeLocation);
        store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);

        var notExpiredSorted = new X509Certificate2Collection(
            (from cert in store.Certificates.Find(X509FindType.FindByTimeValid, DateTime.Now, false)
             orderby string.IsNullOrEmpty(cert.FriendlyName)
                 ? cert.GetNameInfo(X509NameType.SimpleName, false)
                 : cert.FriendlyName descending
             select cert).ToArray());

        var selected = X509Certificate2UI.SelectFromCollection(
            notExpiredSorted,
            Globals.AppName,
            $"Select a certificate from the {storeLocation}\\{storeName} store to add to {vaultAlias}",
            X509SelectionFlag.SingleSelection);

        return Task.FromResult(selected.Count == 1 ? selected[0] : (X509Certificate2?)null);
    }
}
