namespace Microsoft.Vault.Explorer.Controls.MenuItems
{
    using Azure.Security.KeyVault.Certificates;
    using Microsoft.Vault.Library;

    public class CertificateVersion : CustomVersion
    {
        public readonly CertificateProperties CertificateItem;

        public CertificateVersion(int index, CertificateProperties certificateItem) : base(
            index,
            certificateItem.CreatedOn?.UtcDateTime,
            certificateItem.UpdatedOn?.UtcDateTime,
            Library.Utils.GetChangedBy(certificateItem.Tags),
            new ObjectIdentifier(
                certificateItem.Name,
                certificateItem.Id?.ToString() ?? string.Empty,
                certificateItem.Version ?? string.Empty,
                certificateItem.VaultUri?.ToString() ?? string.Empty))
        {
            this.CertificateItem = certificateItem;
        }
    }
}
