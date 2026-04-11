namespace Microsoft.Vault.Explorer.Controls.MenuItems
{
    using Azure.Security.KeyVault.Secrets;
    using Microsoft.Vault.Library;

    public class SecretVersion : CustomVersion
    {
        public readonly SecretProperties SecretItem;

        public SecretVersion(int index, SecretProperties secretItem) : base(
            index,
            secretItem.CreatedOn?.UtcDateTime,
            secretItem.UpdatedOn?.UtcDateTime,
            Library.Utils.GetChangedBy(secretItem.Tags),
            new ObjectIdentifier(
                secretItem.Name,
                secretItem.Id?.ToString() ?? string.Empty,
                secretItem.Version ?? string.Empty,
                secretItem.VaultUri?.ToString() ?? string.Empty))
        {
            this.SecretItem = secretItem;
        }
    }
}
