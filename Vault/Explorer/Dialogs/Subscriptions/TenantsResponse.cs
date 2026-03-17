namespace Microsoft.Vault.Explorer.Dialogs.Subscriptions
{
    using System;
    using Newtonsoft.Json;

    [JsonObject]
    public class TenantsResponse
    {
        [JsonProperty(PropertyName = "value")]
        public TenantItem[] Tenants { get; set; }
    }

    [JsonObject]
    public class TenantItem
    {
        [JsonProperty(PropertyName = "tenantId")]
        public string TenantId { get; set; }

        [JsonProperty(PropertyName = "displayName")]
        public string DisplayName { get; set; }

        [JsonProperty(PropertyName = "domains")]
        public string[] Domains { get; set; }

        public override string ToString()
        {
            string label = !string.IsNullOrWhiteSpace(this.DisplayName) ? this.DisplayName : (this.Domains != null && this.Domains.Length > 0 ? this.Domains[0] : "Tenant");
            return $"{label} ({this.TenantId ?? Guid.Empty.ToString()})";
        }
    }
}
