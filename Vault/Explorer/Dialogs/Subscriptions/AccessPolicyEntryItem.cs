namespace Microsoft.Vault.Explorer.Dialogs.Subscriptions
{
    using System;
    using System.ComponentModel;
    using System.Drawing.Design;
    using Azure.ResourceManager.KeyVault.Models;
    using Newtonsoft.Json;

    [Editor(typeof(ExpandableObjectConverter), typeof(UITypeEditor))]
    public class AccessPolicyEntryItem
    {
        private readonly KeyVaultAccessPolicy _ape;

        public AccessPolicyEntryItem(int index, KeyVaultAccessPolicy ape)
        {
            this.Index = index;
            this._ape = ape;
        }

        [JsonIgnore]
        public int Index { get; }

        [Description("Application ID of the client making request on behalf of a principal")]
        public Guid? ApplicationId => this._ape.ApplicationId;

        [Description("Object ID of the principal")]
        public Guid ObjectId => Guid.Parse(this._ape.ObjectId);

        [Description("Permissions to keys")]
        public string PermissionsToKeys => string.Join(",", this._ape.Permissions?.Keys ?? Array.Empty<IdentityAccessKeyPermission>());

        [Description("Permissions to secrets")]
        public string PermissionsToSecrets => string.Join(",", this._ape.Permissions?.Secrets ?? Array.Empty<IdentityAccessSecretPermission>());

        [Description("Permissions to certificates")]
        public string PermissionsToCertificates => string.Join(",", this._ape.Permissions?.Certificates ?? Array.Empty<IdentityAccessCertificatePermission>());

        [Description("Tenant ID of the principal")]
        public Guid TenantId => this._ape.TenantId;

        public override string ToString() => JsonConvert.SerializeObject(this, Formatting.Indented);
    }
}
