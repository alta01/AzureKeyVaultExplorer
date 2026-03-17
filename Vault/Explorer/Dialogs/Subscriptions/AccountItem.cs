namespace Microsoft.Vault.Explorer.Dialogs.Subscriptions
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public class AccountItem
    {
        public string DomainHint;
        public string UserAlias;
        public string TenantId; // currently selected tenant
        public string DefaultTenantId;
        public List<TenantItem> KnownTenants = new List<TenantItem>();

        public AccountItem(string domainHint, string userAlias = null, string tenantId = null)
        {
            this.DomainHint = domainHint;
            this.UserAlias = userAlias ?? Globals.DefaultUserName;
            this.TenantId = tenantId?.Trim();
            this.DefaultTenantId = this.TenantId;
        }

        public string AccountName
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(this.UserAlias) && this.UserAlias.Contains("@"))
                {
                    return this.UserAlias;
                }

                return $"{this.UserAlias}@{this.DomainHint}";
            }
        }

        public void MergeKnownTenants(IEnumerable<TenantItem> tenants)
        {
            foreach (TenantItem tenant in tenants ?? Enumerable.Empty<TenantItem>())
            {
                if (string.IsNullOrWhiteSpace(tenant?.TenantId))
                {
                    continue;
                }

                TenantItem existing = this.KnownTenants.FirstOrDefault(t => string.Equals(t.TenantId, tenant.TenantId, StringComparison.OrdinalIgnoreCase));
                if (existing == null)
                {
                    this.KnownTenants.Add(tenant);
                }
                else if (string.IsNullOrWhiteSpace(existing.DisplayName) && !string.IsNullOrWhiteSpace(tenant.DisplayName))
                {
                    existing.DisplayName = tenant.DisplayName;
                }
            }
        }

        public override string ToString()
        {
            if (string.IsNullOrWhiteSpace(this.DefaultTenantId))
            {
                return this.AccountName;
            }

            return $"{this.AccountName} (default: {this.DefaultTenantId})";
        }
    }
}
