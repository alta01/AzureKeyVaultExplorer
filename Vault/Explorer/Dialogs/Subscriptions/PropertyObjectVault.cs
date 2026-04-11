namespace Microsoft.Vault.Explorer.Dialogs.Subscriptions
{
    using System;
    using System.ComponentModel;
    using Azure.ResourceManager.KeyVault;
    using Microsoft.Vault.Explorer.Model.Collections;

    public class PropertyObjectVault
    {
        private readonly Subscription _subscription;
        private readonly string _resourceGroupName;
        private readonly KeyVaultResource _vault;

        public PropertyObjectVault(Subscription s, string resourceGroupName, KeyVaultResource vault)
        {
            this._subscription = s;
            this._resourceGroupName = resourceGroupName;
            this._vault = vault;
            this.Tags = new ObservableTagItemsCollection();
            if (null != this._vault.Data.Tags)
            {
                foreach (var kvp in this._vault.Data.Tags)
                {
                    this.Tags.Add(new TagItem(kvp));
                }
            }

            this.AccessPolicies = new ObservableAccessPoliciesCollection();
            int i = -1;
            foreach (var ape in this._vault.Data.Properties.AccessPolicies)
            {
                this.AccessPolicies.Add(new AccessPolicyEntryItem(++i, ape));
            }
        }

        [DisplayName("Name")]
        [ReadOnly(true)]
        public string Name => this._vault.Data.Name;

        [DisplayName("Location")]
        [ReadOnly(true)]
        public string Location => this._vault.Data.Location.ToString();

        [DisplayName("Uri")]
        [ReadOnly(true)]
        public string Uri => this._vault.Data.Properties.VaultUri?.ToString();

        [DisplayName("Subscription Name")]
        [ReadOnly(true)]
        public string SubscriptionName => this._subscription.DisplayName;

        [DisplayName("Subscription Id")]
        [ReadOnly(true)]
        public Guid SubscriptionId => this._subscription.SubscriptionId;

        [DisplayName("Resource Group Name")]
        [ReadOnly(true)]
        public string ResourceGroupName => this._resourceGroupName;

        [DisplayName("Custom Tags")]
        [ReadOnly(true)]
        public ObservableTagItemsCollection Tags { get; private set; }

        [DisplayName("Sku")]
        [ReadOnly(true)]
        public string Sku => this._vault.Data.Properties.Sku.Name.ToString();

        [DisplayName("Access Policies")]
        [ReadOnly(true)]
        [TypeConverter(typeof(ExpandableCollectionObjectConverter))]
        public ObservableAccessPoliciesCollection AccessPolicies { get; }
    }
}
