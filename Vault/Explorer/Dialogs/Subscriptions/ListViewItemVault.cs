namespace Microsoft.Vault.Explorer.Dialogs.Subscriptions
{
    using System.Text.RegularExpressions;
    using System.Windows.Forms;
    using Azure.ResourceManager.KeyVault;

    public class ListViewItemVault : ListViewItem
    {
        // https://azure.microsoft.com/en-us/documentation/articles/guidance-naming-conventions/
        private static readonly Regex s_resourceNameRegex = new Regex(@".*\/resourceGroups\/(?<GroupName>[a-zA-Z0-9_\-\.]{1,64})\/", RegexOptions.CultureInvariant | RegexOptions.Compiled);

        public readonly KeyVaultResource Vault;
        public readonly string GroupName;

        public ListViewItemVault(KeyVaultResource vault) : base(vault.Data.Name)
        {
            this.Vault = vault;
            this.Name = vault.Data.Name;
            this.GroupName = s_resourceNameRegex.Match(vault.Id.ToString()).Groups["GroupName"].Value;
            this.SubItems.Add(this.GroupName);
            this.ToolTipText = $"Location: {vault.Data.Location}";
            this.ImageIndex = 1;
        }
    }
}
