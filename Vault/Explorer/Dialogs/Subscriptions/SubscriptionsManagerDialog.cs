// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Vault.Explorer.Dialogs.Subscriptions
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Threading.Tasks;
    using System.Windows.Forms;
    using Microsoft.Azure.Management.KeyVault;
    using Microsoft.Identity.Client;
    using Microsoft.Rest;
    using Microsoft.Vault.Explorer.Common;
    using Microsoft.Vault.Explorer.Model.Files.Aliases;
    using Microsoft.Vault.Library;
    using Newtonsoft.Json;
    using Settings = Microsoft.Vault.Explorer.Settings;

    public partial class SubscriptionsManagerDialog : Form
    {
        private const string SubscriptionsApiVersion = "api-version=2016-07-01";
        private const string TenantsApiVersion = "api-version=2020-01-01";
        private const string ManagmentEndpoint = "https://management.azure.com/";
        private const string AddAccountText = "Add New Account";

        private AccountItem _currentAccountItem;
        private AuthenticationResult _currentAuthResult;
        private KeyVaultManagementClient _currentKeyVaultMgmtClient;
        private readonly HttpClient _httpClient;
        private bool _isChangingTenants;
        private bool _isChangingAccounts;

        public VaultAlias CurrentVaultAlias { get; private set; }

        public SubscriptionsManagerDialog()
        {
            this.InitializeComponent();
            UiModernizer.Apply(this);
            this._httpClient = new HttpClient();

            bool hasPreConfiguredAccounts = false;
            foreach (SavedUserAccount savedAccount in Settings.Default.GetSavedUserAccounts())
            {
                if (!TryCreateAccountItem(savedAccount, out AccountItem accountItem))
                {
                    continue;
                }

                this.uxComboBoxAccounts.Items.Add(accountItem);
                hasPreConfiguredAccounts = true;
            }

            this.uxComboBoxAccounts.Items.Add(AddAccountText);

            if (hasPreConfiguredAccounts)
            {
                this.uxComboBoxAccounts.SelectedIndex = 0;
            }
            else
            {
                this.uxComboBoxAccounts.SelectedIndex = -1;
                this.uxComboBoxAccounts.Text = "Select an account or add new...";
            }
        }

        private UxOperation NewUxOperationWithProgress(params ToolStripItem[] controlsToToggle) => new UxOperation(null, this.uxStatusLabel, this.uxProgressBar, this.uxButtonCancelOperation, controlsToToggle);

        private async void uxComboBoxAccounts_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (this._isChangingAccounts)
            {
                return;
            }

            switch (this.uxComboBoxAccounts.SelectedItem)
            {
                case null:
                    return;

                case AddAccountText:
                    await this.AddNewAccountAsync();
                    return;

                case AccountItem account:
                    this._currentAccountItem = account;
                    this.ResetTenantAndVaultViews(clearTenants: true);
                    this.PopulateTenantsFromSavedAccount(account);
                    await this.AuthenticateThenLoadTenantsAndSubscriptionsAsync(account.DefaultTenantId);
                    return;
            }
        }

        private async void uxComboBoxTenants_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (this._isChangingTenants)
            {
                return;
            }

            if (!(this.uxComboBoxTenants.SelectedItem is TenantItem tenant))
            {
                return;
            }

            await this.SwitchTenantAndLoadSubscriptionsAsync(tenant.TenantId);
        }

        private async void uxListViewSubscriptions_SelectedIndexChanged(object sender, EventArgs e)
        {
            ListViewItemSubscription s = this.uxListViewSubscriptions.SelectedItems.Count > 0 ? (ListViewItemSubscription)this.uxListViewSubscriptions.SelectedItems[0] : null;
            if (s == null)
            {
                return;
            }

            using (var op = this.NewUxOperationWithProgress(this.uxComboBoxAccounts, this.uxComboBoxTenants))
            {
                var tvcc = new TokenCredentials(this._currentAuthResult.AccessToken);
                this._currentKeyVaultMgmtClient = new KeyVaultManagementClient(tvcc) { SubscriptionId = s.Subscription.SubscriptionId.ToString() };
                var vaults = await this._currentKeyVaultMgmtClient.Vaults.ListAsync(null, op.CancellationToken);
                this.uxListViewVaults.Items.Clear();
                foreach (var v in vaults)
                {
                    this.uxListViewVaults.Items.Add(new ListViewItemVault(v));
                }
            }
        }

        private async void uxListViewVaults_SelectedIndexChanged(object sender, EventArgs e)
        {
            ListViewItemSubscription s = this.uxListViewSubscriptions.SelectedItems.Count > 0 ? (ListViewItemSubscription)this.uxListViewSubscriptions.SelectedItems[0] : null;
            ListViewItemVault v = this.uxListViewVaults.SelectedItems.Count > 0 ? (ListViewItemVault)this.uxListViewVaults.SelectedItems[0] : null;
            this.uxButtonOK.Enabled = false;
            if (s == null || v == null)
            {
                return;
            }

            using (var op = this.NewUxOperationWithProgress(this.uxComboBoxAccounts, this.uxComboBoxTenants))
            {
                var vault = await this._currentKeyVaultMgmtClient.Vaults.GetAsync(v.GroupName, v.Name);
                this.uxPropertyGridVault.SelectedObject = new PropertyObjectVault(s.Subscription, v.GroupName, vault);
                this.uxButtonOK.Enabled = true;

                this.CurrentVaultAlias = new VaultAlias(v.Name, new[] { v.Name }, new[] { "Custom" })
                {
                    DomainHint = this._currentAccountItem.DomainHint,
                    UserAlias = this._currentAccountItem.UserAlias,
                    TenantId = this._currentAccountItem.TenantId,
                    IsNew = true,
                };
            }
        }

        private async Task AddNewAccountAsync()
        {
            this.ResetTenantAndVaultViews(clearTenants: true);
            this._currentAccountItem = new AccountItem("common");
            if (!await this.AuthenticateThenLoadTenantsAndSubscriptionsAsync())
            {
                return;
            }

            if (string.IsNullOrEmpty(this._currentAuthResult?.Account?.Username))
            {
                MessageBox.Show("Authentication did not return a user name. Please try again.", "Authentication Problem", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            this._currentAccountItem.TenantId = NormalizeTenantIdentifier(this._currentAuthResult.TenantId);
            this.EnsureCurrentAccountInCombo();
            this.PersistCurrentAccountSelection();
        }

        private async Task<bool> AuthenticateThenLoadTenantsAndSubscriptionsAsync(string preferredTenantId = null)
        {
            this.ResetTenantAndVaultViews();
            string tenantForSignIn = NormalizeTenantIdentifier(preferredTenantId);
            if (!await this.AuthenticateIntoTenantAsync(tenantForSignIn))
            {
                return false;
            }

            if (!string.IsNullOrEmpty(this._currentAuthResult?.Account?.Username))
            {
                string[] userLogin = this._currentAuthResult.Account.Username.Split('@');
                if (userLogin.Length == 2)
                {
                    this._currentAccountItem.UserAlias = userLogin[0];
                    this._currentAccountItem.DomainHint = userLogin[1];
                }
            }

            TenantItem[] tenants = await this.LoadTenantsAsync(this._currentAccountItem.DefaultTenantId);
            if (tenants == null)
            {
                return false;
            }

            this._currentAccountItem.MergeKnownTenants(tenants);
            this.PersistCurrentAccountSelection();

            string selectedTenant = this.uxComboBoxTenants.SelectedItem is TenantItem tenant
                ? tenant.TenantId
                : null;
            if (!string.IsNullOrWhiteSpace(selectedTenant) &&
                string.Equals(this._currentAuthResult?.TenantId, selectedTenant, StringComparison.OrdinalIgnoreCase))
            {
                await this.SwitchTenantAndLoadSubscriptionsAsync(selectedTenant, promptToSaveDefaultTenant: false);
            }

            return true;
        }

        private async Task<bool> AuthenticateIntoTenantAsync(string tenantId)
        {
            string authorityHint = string.IsNullOrWhiteSpace(tenantId) ? "common" : tenantId;
            string userLoginHint = this.GetUserLoginHint();
            string userAliasForCache = this._currentAccountItem?.UserAlias;
            if (!string.IsNullOrWhiteSpace(userLoginHint) && userAliasForCache.Contains("@") == false)
            {
                userAliasForCache = userLoginHint;
            }

            try
            {
                var vaui = new VaultAccessUserInteractive(authorityHint, userLoginHint, tenantId);
                string[] scopes = VaultAccess.ConvertResourceToScopes(ManagmentEndpoint);
                this._currentAuthResult = await vaui.AcquireTokenAsync(scopes, userAliasForCache);
                return true;
            }
            catch (MsalException ex)
            {
                MessageBox.Show($"Authentication failed: {ex.Message}", "Authentication Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        private async Task<TenantItem[]> LoadTenantsAsync(string preferredTenantId)
        {
            using (var op = this.NewUxOperationWithProgress(this.uxComboBoxAccounts, this.uxComboBoxTenants))
            {
                this._httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", this._currentAuthResult.AccessToken);
                var response = await this._httpClient.GetAsync($"{ManagmentEndpoint}tenants?{TenantsApiVersion}", op.CancellationToken);
                string json = await response.Content.ReadAsStringAsync();
                response.EnsureSuccessStatusCode();

                var tenants = JsonConvert.DeserializeObject<TenantsResponse>(json)?.Tenants ?? Array.Empty<TenantItem>();
                if (tenants.Length == 0)
                {
                    MessageBox.Show("No tenants were returned for this account.", "Tenant Discovery", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return null;
                }

                this.PopulateTenants(tenants, preferredTenantId);
                return tenants;
            }
        }

        private async Task SwitchTenantAndLoadSubscriptionsAsync(string tenantId, bool promptToSaveDefaultTenant = true)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
            {
                return;
            }

            bool alreadyAuthenticatedForTenant = string.Equals(this._currentAuthResult?.TenantId, tenantId, StringComparison.OrdinalIgnoreCase);
            if (!alreadyAuthenticatedForTenant && !await this.AuthenticateIntoTenantAsync(tenantId))
            {
                return;
            }

            this._currentAccountItem.TenantId = tenantId;
            this.PersistCurrentAccountSelection();
            this.EnsureCurrentAccountInCombo();

            using (var op = this.NewUxOperationWithProgress(this.uxComboBoxAccounts, this.uxComboBoxTenants))
            {
                this._httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", this._currentAuthResult.AccessToken);
                var response = await this._httpClient.GetAsync($"{ManagmentEndpoint}subscriptions?{SubscriptionsApiVersion}", op.CancellationToken);
                string json = await response.Content.ReadAsStringAsync();
                response.EnsureSuccessStatusCode();
                var subs = JsonConvert.DeserializeObject<SubscriptionsResponse>(json);

                this.uxListViewSubscriptions.Items.Clear();
                this.uxListViewVaults.Items.Clear();
                this.uxPropertyGridVault.SelectedObject = null;
                foreach (var s in subs?.Subscriptions ?? Array.Empty<Subscription>())
                {
                    this.uxListViewSubscriptions.Items.Add(new ListViewItemSubscription(s));
                }
            }

            if (promptToSaveDefaultTenant)
            {
                this.PromptToSaveDefaultTenant(tenantId);
            }
        }

        private void PersistCurrentAccountSelection()
        {
            string accountName = this._currentAccountItem?.AccountName;
            if (string.IsNullOrWhiteSpace(accountName))
            {
                return;
            }

            string defaultTenant = NormalizeTenantIdentifier(this._currentAccountItem.DefaultTenantId);
            var knownTenants = this._currentAccountItem.KnownTenants
                .Where(t => !string.IsNullOrWhiteSpace(NormalizeTenantIdentifier(t.TenantId)))
                .Select(t => new SavedTenantInfo
                {
                    TenantId = NormalizeTenantIdentifier(t.TenantId),
                    DisplayName = t.DisplayName,
                })
                .ToList();

            Settings.Default.AddOrUpdateSavedUserAccount(accountName, defaultTenant, knownTenants);
        }

        private void EnsureCurrentAccountInCombo()
        {
            if (this._currentAccountItem == null)
            {
                return;
            }

            for (int i = 0; i < this.uxComboBoxAccounts.Items.Count; i++)
            {
                if (this.uxComboBoxAccounts.Items[i] is AccountItem existing &&
                    string.Equals(existing.AccountName, this._currentAccountItem.AccountName, StringComparison.OrdinalIgnoreCase))
                {
                    existing.TenantId = this._currentAccountItem.TenantId;
                    existing.DefaultTenantId = this._currentAccountItem.DefaultTenantId;
                    existing.MergeKnownTenants(this._currentAccountItem.KnownTenants);
                    this.SetSelectedAccountIndexSafely(i);
                    return;
                }
            }

            int addNewAccountIndex = this.uxComboBoxAccounts.Items.Cast<object>().ToList().FindIndex(i => i is string itemText && string.Equals(itemText, AddAccountText, StringComparison.Ordinal));
            int insertIndex = addNewAccountIndex >= 0 ? addNewAccountIndex : this.uxComboBoxAccounts.Items.Count;
            this.uxComboBoxAccounts.Items.Insert(insertIndex, this._currentAccountItem);
            this.SetSelectedAccountIndexSafely(insertIndex);
        }

        private void SetSelectedAccountIndexSafely(int index)
        {
            this._isChangingAccounts = true;
            try
            {
                this.uxComboBoxAccounts.SelectedIndex = index;
            }
            finally
            {
                this._isChangingAccounts = false;
            }
        }

        private string GetUserLoginHint()
        {
            if (this._currentAccountItem == null || string.IsNullOrWhiteSpace(this._currentAccountItem.UserAlias))
            {
                return string.Empty;
            }

            if (this._currentAccountItem.UserAlias.Contains("@"))
            {
                return this._currentAccountItem.UserAlias;
            }

            if (!string.IsNullOrWhiteSpace(this._currentAccountItem.DomainHint) && !string.Equals(this._currentAccountItem.DomainHint, "common", StringComparison.OrdinalIgnoreCase))
            {
                return $"{this._currentAccountItem.UserAlias}@{this._currentAccountItem.DomainHint}";
            }

            return this._currentAccountItem.UserAlias;
        }

        private void ResetTenantAndVaultViews(bool clearTenants = false)
        {
            this.uxListViewSubscriptions.Items.Clear();
            this.uxListViewVaults.Items.Clear();
            this.uxPropertyGridVault.SelectedObject = null;
            this.uxButtonOK.Enabled = false;
            if (clearTenants)
            {
                this._isChangingTenants = true;
                try
                {
                    this.uxComboBoxTenants.Items.Clear();
                    this.uxComboBoxTenants.SelectedIndex = -1;
                    this.uxComboBoxTenants.Text = string.Empty;
                }
                finally
                {
                    this._isChangingTenants = false;
                }
            }
        }

        private void PopulateTenantsFromSavedAccount(AccountItem account)
        {
            if (account == null || account.KnownTenants.Count == 0)
            {
                return;
            }

            this.PopulateTenants(account.KnownTenants.ToArray(), account.DefaultTenantId);
        }

        private void PopulateTenants(IEnumerable<TenantItem> tenants, string preferredTenantId)
        {
            TenantItem[] orderedTenants = (tenants ?? Array.Empty<TenantItem>())
                .Where(t => !string.IsNullOrWhiteSpace(t?.TenantId))
                .OrderBy(t => t.DisplayName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            this._isChangingTenants = true;
            try
            {
                this.uxComboBoxTenants.Items.Clear();
                foreach (TenantItem tenant in orderedTenants)
                {
                    this.uxComboBoxTenants.Items.Add(tenant);
                }

                string preferred = NormalizeTenantIdentifier(preferredTenantId);
                TenantItem selectedTenant = orderedTenants.FirstOrDefault(t => string.Equals(t.TenantId, preferred, StringComparison.OrdinalIgnoreCase));
                if (selectedTenant != null)
                {
                    this.uxComboBoxTenants.SelectedItem = this.uxComboBoxTenants.Items.Cast<object>().FirstOrDefault(i => i is TenantItem ti && string.Equals(ti.TenantId, selectedTenant.TenantId, StringComparison.OrdinalIgnoreCase));
                }
                else
                {
                    this.uxComboBoxTenants.SelectedIndex = -1;
                }
            }
            finally
            {
                this._isChangingTenants = false;
            }
        }

        private void PromptToSaveDefaultTenant(string selectedTenantId)
        {
            string normalizedTenant = NormalizeTenantIdentifier(selectedTenantId);
            if (string.IsNullOrWhiteSpace(normalizedTenant) || this._currentAccountItem == null)
            {
                return;
            }

            if (string.Equals(this._currentAccountItem.DefaultTenantId, normalizedTenant, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            DialogResult result = MessageBox.Show(
                $"Use tenant '{normalizedTenant}' as the default sign-in tenant for '{this._currentAccountItem.AccountName}'?",
                "Set Default Tenant",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            if (result != DialogResult.Yes)
            {
                return;
            }

            this._currentAccountItem.DefaultTenantId = normalizedTenant;
            this.PersistCurrentAccountSelection();
            this.EnsureCurrentAccountInCombo();
        }

        private static bool TryCreateAccountItem(SavedUserAccount savedAccount, out AccountItem accountItem)
        {
            accountItem = null;
            if (savedAccount == null || !TrySplitAccountName(savedAccount.AccountName, out string userAlias, out string domainHint))
            {
                return false;
            }

            accountItem = new AccountItem(domainHint, userAlias, savedAccount.DefaultTenantId)
            {
                DefaultTenantId = NormalizeTenantIdentifier(savedAccount.DefaultTenantId),
            };

            foreach (SavedTenantInfo tenant in savedAccount.KnownTenants ?? new List<SavedTenantInfo>())
            {
                string tenantId = NormalizeTenantIdentifier(tenant.TenantId);
                if (string.IsNullOrWhiteSpace(tenantId))
                {
                    continue;
                }

                accountItem.KnownTenants.Add(new TenantItem
                {
                    TenantId = tenantId,
                    DisplayName = tenant.DisplayName,
                });
            }

            return true;
        }

        private static bool TrySplitAccountName(string accountName, out string userAlias, out string domainHint)
        {
            userAlias = null;
            domainHint = null;
            string value = accountName?.Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string[] accounts = value.Split('@');
            if (accounts.Length != 2)
            {
                return false;
            }

            userAlias = accounts[0];
            domainHint = accounts[1];
            return true;
        }

        private static string NormalizeTenantIdentifier(string tenantId)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
            {
                return null;
            }

            string normalized = tenantId.Trim();
            if (normalized.Length > 128)
            {
                return null;
            }

            return normalized.All(c => char.IsLetterOrDigit(c) || c == '.' || c == '-') ? normalized : null;
        }
    }
}
