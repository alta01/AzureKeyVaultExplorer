// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Vault.Explorer.ViewModels
{
    using System;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Reactive;
    using System.Reactive.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.ResourceManager;
    using Azure.ResourceManager.KeyVault;
    using Azure.ResourceManager.Resources;
    using Microsoft.Identity.Client;
    using Microsoft.Vault.Explorer.Dialogs.Subscriptions;
    using Microsoft.Vault.Explorer.Model.Files.Aliases;
    using Microsoft.Vault.Explorer.Services;
    using Microsoft.Vault.Library;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using ReactiveUI;

    /// <summary>
    /// ViewModel for the Subscriptions Manager dialog.
    /// Replaces the WinForms <see cref="Dialogs.Subscriptions.SubscriptionsManagerDialog"/>.
    /// </summary>
    public sealed class SubscriptionsManagerViewModel : ViewModelBase, IDisposable
    {
        private const string ManagementEndpoint = "https://management.azure.com/";
        private const string ApiVersion = "api-version=2016-07-01";
        private const string TenantsApiVersion = "api-version=2020-01-01";

        private readonly IDialogService _dialogs;
        private readonly HttpClient _httpClient = new();

        // ── Auth state ─────────────────────────────────────────────────────────
        private AccountItem? _currentAccount;
        private AuthenticationResult? _currentAuth;
        private ArmClient? _currentArmClient;

        // ── Accounts ───────────────────────────────────────────────────────────
        public ObservableCollection<object> Accounts { get; } = new(); // AccountItem | "Add New Account"
        private const string AddAccountText = "Add New Account";

        private object? _selectedAccount;
        public object? SelectedAccount
        {
            get => _selectedAccount;
            set
            {
                this.RaiseAndSetIfChanged(ref _selectedAccount, value);
                _ = OnAccountSelectedAsync(value);
            }
        }

        // ── Tenants ────────────────────────────────────────────────────────────
        public ObservableCollection<TenantItem> Tenants { get; } = new();

        private TenantItem? _selectedTenant;
        public TenantItem? SelectedTenant
        {
            get => _selectedTenant;
            set
            {
                this.RaiseAndSetIfChanged(ref _selectedTenant, value);
                _ = OnTenantSelectedAsync(value);
            }
        }

        // ── Subscriptions ──────────────────────────────────────────────────────
        public ObservableCollection<SubscriptionItem> Subscriptions { get; } = new();

        private SubscriptionItem? _selectedSubscription;
        public SubscriptionItem? SelectedSubscription
        {
            get => _selectedSubscription;
            set
            {
                this.RaiseAndSetIfChanged(ref _selectedSubscription, value);
                _ = LoadVaultsAsync(value);
            }
        }

        // ── Vaults ─────────────────────────────────────────────────────────────
        public ObservableCollection<VaultItem> Vaults { get; } = new();

        private VaultItem? _selectedVault;
        public VaultItem? SelectedVault
        {
            get => _selectedVault;
            set
            {
                this.RaiseAndSetIfChanged(ref _selectedVault, value);
                _ = LoadVaultDetailsAsync(value);
            }
        }

        // ── Selected vault details ─────────────────────────────────────────────
        private PropertyObjectVault? _vaultDetails;
        public PropertyObjectVault? VaultDetails
        {
            get => _vaultDetails;
            private set => this.RaiseAndSetIfChanged(ref _vaultDetails, value);
        }

        // ── Busy / status ──────────────────────────────────────────────────────
        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            private set => this.RaiseAndSetIfChanged(ref _isBusy, value);
        }

        private string _statusText = "";
        public string StatusText
        {
            get => _statusText;
            private set => this.RaiseAndSetIfChanged(ref _statusText, value);
        }

        // ── Result ─────────────────────────────────────────────────────────────
        private VaultAlias? _currentVaultAlias;
        public VaultAlias? CurrentVaultAlias
        {
            get => _currentVaultAlias;
            private set
            {
                this.RaiseAndSetIfChanged(ref _currentVaultAlias, value);
                CanConfirm = value != null;
            }
        }

        private bool _canConfirm;
        public bool CanConfirm
        {
            get => _canConfirm;
            private set => this.RaiseAndSetIfChanged(ref _canConfirm, value);
        }

        // ── Commands ───────────────────────────────────────────────────────────
        public ReactiveCommand<Unit, VaultAlias?> ConfirmCommand { get; }

        // ── Constructor ────────────────────────────────────────────────────────

        public SubscriptionsManagerViewModel(IDialogService dialogs)
        {
            _dialogs = dialogs;

            ConfirmCommand = ReactiveCommand.Create(
                () => CurrentVaultAlias,
                this.WhenAnyValue(x => x.CanConfirm));

            // Populate accounts from saved settings
            bool hasAccounts = false;
            foreach (string accountName in AppSettings.Default.UserAccountNamesList)
            {
                string[] parts = accountName.Split('@');
                if (parts.Length < 2) continue;
                Accounts.Add(new AccountItem(parts[1], parts[0]));
                hasAccounts = true;
            }
            Accounts.Add(AddAccountText);

            // Show onboarding message if no accounts configured
            if (!hasAccounts)
            {
                _ = ShowOnboardingAsync();
            }
        }

        // ── Private async handlers ─────────────────────────────────────────────

        private async Task ShowOnboardingAsync()
        {
            await Task.Delay(200); // Let the window render first
            await _dialogs.ShowMessageAsync(
                "Subscriptions onboarding",
                "No saved accounts were found.\n\nSelect 'Add New Account' to sign in, then choose a subscription and vault.");
        }

        private async Task OnAccountSelectedAsync(object? item)
        {
            if (item == null) return;

            if (item is string s && s == AddAccountText)
            {
                await AddNewAccountAsync();
                return;
            }

            if (item is not AccountItem account) return;

            _currentAccount = account;
            await AuthenticateAsync();
            if (_currentAuth != null) await LoadTenantsAsync();
        }

        private async Task OnTenantSelectedAsync(TenantItem? tenant)
        {
            if (tenant == null || _currentAccount == null) return;
            if (string.Equals(_currentAccount.DomainHint, tenant.TenantId, StringComparison.OrdinalIgnoreCase)) return;

            _currentAccount.DomainHint = tenant.TenantId;
            await AuthenticateAsync();
            if (_currentAuth != null) await LoadSubscriptionsAsync();
        }

        private async Task AuthenticateAsync()
        {
            if (_currentAccount == null) return;
            IsBusy = true;
            StatusText = "Authenticating…";
            try
            {
                var vaui = new VaultAccessUserInteractive(_currentAccount.DomainHint, _currentAccount.UserAlias);
                string[] scopes = VaultAccess.ConvertResourceToScopes(ManagementEndpoint);
                _currentAuth = await vaui.AcquireTokenAsync(scopes, _currentAccount.UserAlias);

                if (_currentAuth.Account?.Username != null)
                    _currentAccount.UserAlias = _currentAuth.Account.Username;
                if (!string.IsNullOrWhiteSpace(_currentAuth.TenantId))
                    _currentAccount.DomainHint = _currentAuth.TenantId;
            }
            catch (MsalException ex)
            {
                await _dialogs.ShowErrorAsync(
                    "Authentication Error",
                    $"Authentication failed: {ex.Message}\n\nClose any open sign-in browser windows and try again.",
                    ex);
                _currentAuth = null;
                SelectedAccount = null;
            }
            catch (Exception ex)
            {
                await _dialogs.ShowErrorAsync("Authentication Error", ex.Message, ex);
                _currentAuth = null;
                SelectedAccount = null;
            }
            finally
            {
                IsBusy = false;
                StatusText = "";
            }
        }

        private async Task LoadTenantsAsync()
        {
            if (_currentAuth == null) return;
            IsBusy = true;
            StatusText = "Loading tenants…";
            try
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", _currentAuth.AccessToken);
                var hrm = await _httpClient.GetAsync(
                    $"{ManagementEndpoint}tenants?{TenantsApiVersion}");
                hrm.EnsureSuccessStatusCode();
                var json = await hrm.Content.ReadAsStringAsync();
                var payload = JObject.Parse(json);
                var tenants = payload["value"]?
                    .Select(v => new TenantItem(
                        (string?)v["tenantId"] ?? "",
                        (string?)v["displayName"] ?? "",
                        (string?)v["defaultDomain"] ?? ""))
                    .Where(t => !string.IsNullOrWhiteSpace(t.TenantId))
                    .ToList();

                Tenants.Clear();
                if (tenants != null)
                    foreach (var t in tenants)
                        Tenants.Add(t);

                // Pre-select the tenant matching the auth result
                var selectedTenant = Tenants.FirstOrDefault(t =>
                    string.Equals(t.TenantId, _currentAuth.TenantId, StringComparison.OrdinalIgnoreCase))
                    ?? Tenants.FirstOrDefault();

                _selectedTenant = selectedTenant; // Set backing field directly to avoid re-triggering LoadSubscriptions
                this.RaisePropertyChanged(nameof(SelectedTenant));

                await LoadSubscriptionsAsync();
            }
            catch (Exception ex)
            {
                await _dialogs.ShowErrorAsync("Tenant load error", $"Failed to load tenants: {ex.Message}", ex);
            }
            finally
            {
                IsBusy = false;
                StatusText = "";
            }
        }

        private async Task LoadSubscriptionsAsync()
        {
            if (_currentAuth == null) return;
            IsBusy = true;
            StatusText = "Loading subscriptions…";
            try
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", _currentAuth.AccessToken);
                var hrm = await _httpClient.GetAsync(
                    $"{ManagementEndpoint}subscriptions?{ApiVersion}");
                hrm.EnsureSuccessStatusCode();
                var json = await hrm.Content.ReadAsStringAsync();
                var subs = JsonConvert.DeserializeObject<SubscriptionsResponse>(json);

                Subscriptions.Clear();
                Vaults.Clear();
                VaultDetails = null;
                CurrentVaultAlias = null;

                if (subs?.Subscriptions == null || subs.Subscriptions.Length == 0)
                {
                    await _dialogs.ShowMessageAsync(
                        "No subscriptions found",
                        "No subscriptions were found for this account.\nConfirm the account has access and try another account or tenant.");
                    return;
                }

                foreach (var s in subs.Subscriptions)
                    Subscriptions.Add(new SubscriptionItem(s));
            }
            catch (Exception ex)
            {
                await _dialogs.ShowErrorAsync("Subscriptions error",
                    $"Failed to load subscriptions: {ex.Message}", ex);
            }
            finally
            {
                IsBusy = false;
                StatusText = "";
            }
        }

        private async Task LoadVaultsAsync(SubscriptionItem? subItem)
        {
            if (subItem == null || _currentAuth == null) return;
            IsBusy = true;
            StatusText = $"Loading vaults for {subItem.Name}…";
            try
            {
                var cred = new StaticTokenCredential(_currentAuth.AccessToken, _currentAuth.ExpiresOn);
                _currentArmClient = new ArmClient(cred, subItem.Subscription.SubscriptionId.ToString());
                var subResource = _currentArmClient.GetSubscriptionResource(
                    SubscriptionResource.CreateResourceIdentifier(subItem.Subscription.SubscriptionId.ToString()));

                Vaults.Clear();
                VaultDetails = null;
                CurrentVaultAlias = null;

                await foreach (var v in subResource.GetKeyVaultsAsync())
                    Vaults.Add(new VaultItem(v, subItem));
            }
            catch (Exception ex)
            {
                await _dialogs.ShowErrorAsync("Vault list error",
                    $"Failed to load vaults: {ex.Message}", ex);
            }
            finally
            {
                IsBusy = false;
                StatusText = "";
            }
        }

        private async Task LoadVaultDetailsAsync(VaultItem? vaultItem)
        {
            CurrentVaultAlias = null;
            VaultDetails = null;
            if (vaultItem == null || _currentAccount == null) return;

            IsBusy = true;
            StatusText = $"Loading {vaultItem.Name} details…";
            try
            {
                var vault = (await vaultItem.Resource.GetAsync()).Value;
                VaultDetails = new PropertyObjectVault(
                    vaultItem.SubscriptionItem.Subscription,
                    vaultItem.GroupName,
                    vault);

                CurrentVaultAlias = new VaultAlias(
                    vaultItem.Name,
                    new[] { vaultItem.Name },
                    new[] { "Custom" })
                {
                    DomainHint = _currentAccount.DomainHint,
                    UserAlias = _currentAccount.UserAlias,
                    IsNew = true,
                };
            }
            catch (Exception ex)
            {
                await _dialogs.ShowErrorAsync("Vault load error",
                    $"Failed to load vault details: {ex.Message}", ex);
            }
            finally
            {
                IsBusy = false;
                StatusText = "";
            }
        }

        private async Task AddNewAccountAsync()
        {
            _currentAccount = new AccountItem("common");
            await AuthenticateAsync();
            if (_currentAuth?.Account?.Username == null)
            {
                await _dialogs.ShowMessageAsync(
                    "Authentication Problem",
                    "Authentication did not return a user name. Please try again.");
                _selectedAccount = null;
                this.RaisePropertyChanged(nameof(SelectedAccount));
                return;
            }

            string userAccountName = _currentAuth.Account.Username;
            string[] parts = userAccountName.Split('@');
            if (parts.Length != 2)
            {
                await _dialogs.ShowMessageAsync(
                    "Unsupported account format",
                    "Could not parse signed-in account name. Please sign in with a standard UPN account.");
                return;
            }

            _currentAccount.UserAlias = userAccountName;
            _currentAccount.DomainHint = string.IsNullOrWhiteSpace(_currentAuth.TenantId)
                ? parts[1] : _currentAuth.TenantId;

            if (!AppSettings.Default.AddUserAccountName(userAccountName))
            {
                // Already exists — find and select it
                var existing = Accounts.OfType<AccountItem>()
                    .FirstOrDefault(a => string.Equals(a.ToString(), userAccountName, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                    SelectedAccount = existing;
                await _dialogs.ShowMessageAsync(
                    "Account already exists",
                    $"The account {userAccountName} is already configured and has been selected.");
                return;
            }

            var newItem = new AccountItem(_currentAccount.DomainHint, _currentAccount.UserAlias);
            Accounts.Insert(0, newItem);
            _selectedAccount = newItem;
            this.RaisePropertyChanged(nameof(SelectedAccount));
        }

        public void Dispose() => _httpClient.Dispose();
    }

    // ── Supporting display models ──────────────────────────────────────────────

    public sealed class TenantItem
    {
        public string TenantId { get; }
        public string DisplayName { get; }
        public string Domain { get; }

        public TenantItem(string tenantId, string displayName, string domain)
        {
            TenantId = tenantId;
            DisplayName = displayName;
            Domain = domain;
        }

        public override string ToString()
        {
            string name = !string.IsNullOrWhiteSpace(DisplayName) ? DisplayName : Domain;
            return string.IsNullOrWhiteSpace(name) ? TenantId : $"{name} ({TenantId})";
        }
    }

    public sealed class SubscriptionItem
    {
        public readonly Subscription Subscription;
        public string Name => Subscription.DisplayName ?? Subscription.SubscriptionId.ToString();

        public SubscriptionItem(Subscription sub) => Subscription = sub;
        public override string ToString() => Name;
    }

    public sealed class VaultItem
    {
        public readonly KeyVaultResource Resource;
        public readonly SubscriptionItem SubscriptionItem;
        public string Name => Resource.Data.Name;
        public string GroupName => Resource.Id.ResourceGroupName;

        public VaultItem(KeyVaultResource res, SubscriptionItem sub)
        {
            Resource = res;
            SubscriptionItem = sub;
        }

        public override string ToString() => $"{Name} ({GroupName})";
    }
}
