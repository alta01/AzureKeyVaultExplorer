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
    using Newtonsoft.Json.Linq;
    using Newtonsoft.Json;
    using Settings = Microsoft.Vault.Explorer.Settings;
    using Utils = Microsoft.Vault.Explorer.Common.Utils;

    public partial class SubscriptionsManagerDialog : Form
    {
        private const string ApiVersion = "api-version=2016-07-01";
        private const string ManagmentEndpoint = "https://management.azure.com/";
        private const string TenantsApiVersion = "api-version=2020-01-01";
        private const string AddAccountText = "Add New Account";
        private const string SelectAccountPrompt = "Select an account or add new...";
        private const string SelectTenantPrompt = "Select a tenant...";

        private AccountItem _currentAccountItem;
        private AuthenticationResult _currentAuthResult;
        private KeyVaultManagementClient _currentKeyVaultMgmtClient;
        private readonly HttpClient _httpClient;
        private bool _suppressTenantSelectionEvent;
        private bool _showOnboardingOnLoad;

        public VaultAlias CurrentVaultAlias { get; private set; }

        public SubscriptionsManagerDialog()
        {
            this.InitializeComponent();
            this._httpClient = new HttpClient();

            // Create Default accounts based on domain hints and aliases.
            bool hasPreConfiguredAccounts = false;
            foreach (string userAccountName in Settings.Default.UserAccountNamesList)
            {
                string[] accounts = userAccountName.Split('@');
                if (accounts.Length < 2)
                {
                    continue;
                }

                this.uxComboBoxAccounts.Items.Add(new AccountItem(accounts[1], accounts[0]));
                hasPreConfiguredAccounts = true;
            }

            this.uxComboBoxAccounts.Items.Add(AddAccountText);
            this.uxComboBoxTenants.Items.Clear();
            this.uxComboBoxTenants.Items.Add(SelectTenantPrompt);
            this.uxComboBoxTenants.SelectedIndex = 0;

            // Only auto-select if we have pre-configured accounts, otherwise let user choose
            if (hasPreConfiguredAccounts)
            {
                this.uxComboBoxAccounts.SelectedIndex = 0;
            }
            else
            {
                // No pre-configured accounts — defer the onboarding prompt until after the form is shown
                // so the form renders fully before any MessageBox appears.
                this.uxComboBoxAccounts.SelectedIndex = -1;
                this.uxComboBoxAccounts.Text = SelectAccountPrompt;
                this._showOnboardingOnLoad = true;
            }

            this.Shown += this.SubscriptionsManagerDialog_Shown;
        }

        private void SubscriptionsManagerDialog_Shown(object sender, EventArgs e)
        {
            if (this._showOnboardingOnLoad)
            {
                this._showOnboardingOnLoad = false;
                MessageBox.Show(
                    this,
                    "No saved accounts were found.\n\nSelect 'Add New Account' to sign in, then choose a subscription and vault.",
                    "Subscriptions onboarding",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
        }

        private UxOperation NewUxOperationWithProgress(params ToolStripItem[] controlsToToggle) => new UxOperation(null, this.uxStatusLabel, this.uxProgressBar, this.uxButtonCancelOperation, controlsToToggle);

        private async void uxComboBoxAccounts_SelectedIndexChanged(object sender, EventArgs e)
        {
            switch (this.uxComboBoxAccounts.SelectedItem)
            {
                case null:
                    return;

                case AddAccountText:
                    this.AddNewAccount();
                    break;

                case AccountItem account:
                    // Authenticate into selected account
                    this._currentAccountItem = account;
                    await this.GetAuthenticationTokenAsync();
                    if (this._currentAuthResult.Account != null)
                    {
                        this._currentAccountItem.UserAlias = this._currentAuthResult.Account.Username;
                    }

                    if (!string.IsNullOrWhiteSpace(this._currentAuthResult?.TenantId))
                    {
                        this._currentAccountItem.DomainHint = this._currentAuthResult.TenantId;
                    }

                    break;

                default:
                    return;
            }

            if (this._currentAuthResult == null)
            {
                return;
            }

            await this.LoadTenantsAsync();
            await this.LoadSubscriptionsAsync();
        }

        private async Task LoadTenantsAsync()
        {
            using (var op = this.NewUxOperationWithProgress(this.uxComboBoxAccounts, this.uxComboBoxTenants))
            {
                try
                {
                    this._httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", this._currentAuthResult.AccessToken);
                    var hrm = await this._httpClient.GetAsync($"{ManagmentEndpoint}tenants?{TenantsApiVersion}", op.CancellationToken);
                    var json = await hrm.Content.ReadAsStringAsync();
                    hrm.EnsureSuccessStatusCode();
                    JObject payload = JObject.Parse(json);
                    List<TenantItem> tenants = payload["value"]?
                        .Select(v => new TenantItem((string)v["tenantId"], (string)v["displayName"], (string)v["defaultDomain"]))
                        .Where(t => !string.IsNullOrWhiteSpace(t.TenantId))
                        .ToList() ?? new List<TenantItem>();

                    if (tenants.Count == 0)
                    {
                        return;
                    }

                    this._suppressTenantSelectionEvent = true;
                    this.uxComboBoxTenants.Items.Clear();
                    foreach (TenantItem tenant in tenants)
                    {
                        this.uxComboBoxTenants.Items.Add(tenant);
                    }

                    TenantItem selectedTenant = tenants.FirstOrDefault(t => string.Equals(t.TenantId, this._currentAuthResult.TenantId, StringComparison.OrdinalIgnoreCase))
                                               ?? tenants.FirstOrDefault(t => string.Equals(t.TenantId, this._currentAccountItem.DomainHint, StringComparison.OrdinalIgnoreCase))
                                               ?? tenants[0];
                    this.uxComboBoxTenants.SelectedItem = selectedTenant;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        $"Failed to load tenants: {ex.Message}",
                        "Tenant load error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
                finally
                {
                    this._suppressTenantSelectionEvent = false;
                }
            }
        }

        private async Task LoadSubscriptionsAsync()
        {
            using (var op = this.NewUxOperationWithProgress(this.uxComboBoxAccounts, this.uxComboBoxTenants))
            {
                try
                {
                    this._httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", this._currentAuthResult.AccessToken);
                    var hrm = await this._httpClient.GetAsync($"{ManagmentEndpoint}subscriptions?{ApiVersion}", op.CancellationToken);
                    var json = await hrm.Content.ReadAsStringAsync();
                    hrm.EnsureSuccessStatusCode();
                    var subs = JsonConvert.DeserializeObject<SubscriptionsResponse>(json);

                    this.uxListViewSubscriptions.Items.Clear();
                    this.uxListViewVaults.Items.Clear();
                    this.uxPropertyGridVault.SelectedObject = null;
                    if (subs?.Subscriptions == null || subs.Subscriptions.Length == 0)
                    {
                        MessageBox.Show(
                            "No subscriptions were found for this account.\n\nConfirm the account has access and try another account or tenant.",
                            "No subscriptions found",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);
                        return;
                    }

                    foreach (var s in subs.Subscriptions)
                    {
                        this.uxListViewSubscriptions.Items.Add(new ListViewItemSubscription(s));
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        $"Failed to load subscriptions: {ex.Message}",
                        "Subscriptions error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            }
        }

        private async void uxComboBoxTenants_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (this._suppressTenantSelectionEvent || this.uxComboBoxTenants.SelectedItem is not TenantItem tenant)
            {
                return;
            }

            if (this._currentAccountItem == null || string.Equals(this._currentAccountItem.DomainHint, tenant.TenantId, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            this._currentAccountItem.DomainHint = tenant.TenantId;
            await this.GetAuthenticationTokenAsync();
            await this.LoadSubscriptionsAsync();
        }

        private async void uxListViewSubscriptions_SelectedIndexChanged(object sender, EventArgs e)
        {
            ListViewItemSubscription s = this.uxListViewSubscriptions.SelectedItems.Count > 0 ? (ListViewItemSubscription)this.uxListViewSubscriptions.SelectedItems[0] : null;
            if (null == s)
            {
                return;
            }

            if (this._currentAuthResult == null)
            {
                MessageBox.Show("Please sign in first by selecting an account.", "Not signed in", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (var op = this.NewUxOperationWithProgress(this.uxComboBoxAccounts))
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
            this.CurrentVaultAlias = null;
            if (null == s || null == v)
            {
                return;
            }

            using (var op = this.NewUxOperationWithProgress(this.uxComboBoxAccounts))
            {
                try
                {
                    var vault = await this._currentKeyVaultMgmtClient.Vaults.GetAsync(v.GroupName, v.Name);
                    this.uxPropertyGridVault.SelectedObject = new PropertyObjectVault(s.Subscription, v.GroupName, vault);
                    this.CurrentVaultAlias = new VaultAlias(v.Name, new[] { v.Name }, new[] { "Custom" })
                    {
                        DomainHint = this._currentAccountItem.DomainHint,
                        UserAlias = this._currentAccountItem.UserAlias,
                        IsNew = true,
                    };
                    this.uxButtonOK.Enabled = true;
                }
                catch (Exception ex)
                {
                    this.uxPropertyGridVault.SelectedObject = null;
                    MessageBox.Show(
                        this,
                        $"Failed to load vault details: {ex.Message}\n\nSelect a different vault or try again.",
                        "Vault load error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
            }
        }


        private async void AddNewAccount()
        {
            try
            {
                // For new account, use "common" as domain hint to let Azure AD determine the tenant
                this._currentAccountItem = new AccountItem("common");
                await this.GetAuthenticationTokenAsync();

                if (string.IsNullOrEmpty(this._currentAuthResult.Account?.Username))
                {
                    MessageBox.Show("Authentication did not return a user name. Please try again.", "Authentication Problem", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // Get new user account and add it to default settings
                string userAccountName = this._currentAuthResult.Account.Username;
                string[] userLogin = userAccountName.Split('@');
                if (userLogin.Length != 2)
                {
                    MessageBox.Show("Could not parse signed-in account name. Please sign in with a standard UPN account.", "Unsupported account format", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                this._currentAccountItem.UserAlias = userAccountName;
                this._currentAccountItem.DomainHint = string.IsNullOrWhiteSpace(this._currentAuthResult.TenantId) ? userLogin[1] : this._currentAuthResult.TenantId;
                if (!Settings.Default.AddUserAccountName(userAccountName))
                {
                    AccountItem existing = this.uxComboBoxAccounts.Items.OfType<AccountItem>()
                        .FirstOrDefault(a => string.Equals(a.ToString(), userAccountName, StringComparison.OrdinalIgnoreCase));
                    if (existing != null)
                    {
                        this.uxComboBoxAccounts.SelectedItem = existing;
                    }

                    MessageBox.Show($"The account {userAccountName} is already configured and has been selected.", "Account already exists", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                // Add the new account to the dropdown and select it
                var newAccountItem = new AccountItem(this._currentAccountItem.DomainHint, this._currentAccountItem.UserAlias);
                this.uxComboBoxAccounts.Items.Insert(0, newAccountItem);
                this.uxComboBoxAccounts.SelectedIndex = 0;
            }
            catch (MsalException ex)
            {
                MessageBox.Show(
                    $"Authentication failed: {ex.Message}\n\nTip: close existing browser sign-in windows and try 'Add New Account' again.",
                    "Authentication Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                // Reset selection to allow user to try again
                this.uxComboBoxAccounts.SelectedIndex = -1;
                this.uxComboBoxAccounts.Text = SelectAccountPrompt;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Authentication failed: {ex.Message}", "Authentication Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                // Reset selection to allow user to try again
                this.uxComboBoxAccounts.SelectedIndex = -1;
                this.uxComboBoxAccounts.Text = SelectAccountPrompt;
            }
        }

        // Attempt to authenticate with current account.
        private async Task GetAuthenticationTokenAsync()
        {
            VaultAccessUserInteractive vaui = new VaultAccessUserInteractive(this._currentAccountItem.DomainHint, this._currentAccountItem.UserAlias);
            string[] scopes = VaultAccess.ConvertResourceToScopes(ManagmentEndpoint);
            this._currentAuthResult = await vaui.AcquireTokenAsync(scopes, this._currentAccountItem.UserAlias);
        }

        private sealed class TenantItem
        {
            public string TenantId { get; }
            public string DisplayName { get; }
            public string Domain { get; }

            public TenantItem(string tenantId, string displayName, string domain)
            {
                this.TenantId = tenantId;
                this.DisplayName = displayName;
                this.Domain = domain;
            }

            public override string ToString()
            {
                string name = !string.IsNullOrWhiteSpace(this.DisplayName) ? this.DisplayName : this.Domain;
                return string.IsNullOrWhiteSpace(name) ? this.TenantId : $"{name} ({this.TenantId})";
            }
        }
    }
}
