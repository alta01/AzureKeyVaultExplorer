// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Vault.Explorer
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.ComponentModel.Design;
    using System.Configuration;
    using System.Drawing;
    using System.Drawing.Design;
    using System.Linq;
    using System.Windows.Forms.Design;
    using Microsoft.Vault.Explorer.Controls.Lists.Favorites;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    public class SavedTenantInfo
    {
        [JsonProperty(PropertyName = "tenantId")]
        public string TenantId { get; set; }

        [JsonProperty(PropertyName = "displayName")]
        public string DisplayName { get; set; }
    }

    public class SavedUserAccount
    {
        [JsonProperty(PropertyName = "accountName")]
        public string AccountName { get; set; }

        [JsonProperty(PropertyName = "defaultTenantId")]
        public string DefaultTenantId { get; set; }

        [JsonProperty(PropertyName = "knownTenants")]
        public List<SavedTenantInfo> KnownTenants { get; set; } = new List<SavedTenantInfo>();
    }

    public class Settings : ApplicationSettingsBase
    {
        private static readonly Settings defaultInstance = (Settings)Synchronized(new Settings());
        private readonly FavoriteSecretsDictionary _favoriteSecretsDictionary;

        public static Settings Default
        {
            get { return defaultInstance; }
        }

        public Settings()
        {
            this._favoriteSecretsDictionary = JsonConvert.DeserializeObject<FavoriteSecretsDictionary>(this.FavoriteSecretsJson);
        }

        [UserScopedSetting]
        [DefaultSettingValue("00:00:30")]
        [DisplayName("Clear secret from clipboard after")]
        [Description("Interval for secret to stay in the clipboard once copied to the clipboard.")]
        [Category("General")]
        public TimeSpan CopyToClipboardTimeToLive
        {
            get { return (TimeSpan)this[nameof(this.CopyToClipboardTimeToLive)]; }
            set
            {
                if (value <= TimeSpan.Zero)
                {
                    throw new ArgumentOutOfRangeException(nameof(this.CopyToClipboardTimeToLive));
                }

                this[nameof(this.CopyToClipboardTimeToLive)] = value;
            }
        }

        [UserScopedSetting]
        [DefaultSettingValue("14.00:00:00")]
        [DisplayName("About to expire warning period")]
        [Description("Warning interval to use for items that are close to their expiration date.")]
        [Category("General")]
        public TimeSpan AboutToExpireWarningPeriod
        {
            get { return (TimeSpan)this[nameof(this.AboutToExpireWarningPeriod)]; }
            set { this[nameof(this.AboutToExpireWarningPeriod)] = value; }
        }

        [UserScopedSetting]
        [DefaultSettingValue("Orange")]
        [DisplayName("About to expire item color")]
        [Description("Color to use for items that are close to their expiration date.")]
        [Category("General")]
        public Color AboutToExpireItemColor
        {
            get { return (Color)this[nameof(this.AboutToExpireItemColor)]; }
            set { this[nameof(this.AboutToExpireItemColor)] = value; }
        }

        [UserScopedSetting]
        [DefaultSettingValue("Red")]
        [DisplayName("Expired item color")]
        [Description("Color to use for expired or not yet active item.")]
        [Category("General")]
        public Color ExpiredItemColor
        {
            get { return (Color)this[nameof(this.ExpiredItemColor)]; }
            set { this[nameof(this.ExpiredItemColor)] = value; }
        }

        [UserScopedSetting]
        [DefaultSettingValue("GrayText")]
        [DisplayName("Disabled item color")]
        [Description("Color to use for disabled item.")]
        [Category("General")]
        public Color DisabledItemColor
        {
            get { return (Color)this[nameof(this.DisabledItemColor)]; }
            set { this[nameof(this.DisabledItemColor)] = value; }
        }

        [UserScopedSetting]
        [DisplayName("Root location")]
        [Description("Relative or absolute path to root folder where .json files are located.\nEnvironment variables are supported and expanded accordingly.")]
        [Category("Vaults configuration")]
        [Editor(typeof(FolderNameEditor), typeof(UITypeEditor))]
        public string JsonConfigurationFilesRoot
        {
            get { return (string)this[nameof(this.JsonConfigurationFilesRoot)]; }
            set { this[nameof(this.JsonConfigurationFilesRoot)] = value; }
        }

        [UserScopedSetting]
        [DefaultSettingValue(@"Vaults.json")]
        [DisplayName("Vaults file name")]
        [Description("Relative or absolute path to .json file with vaults definitions and access.\nEnvironment variables are supported and expanded accordingly.")]
        [Category("Vaults configuration")]
        [Editor(typeof(FileNameEditor), typeof(UITypeEditor))]
        public string VaultsJsonFileLocation
        {
            get { return (string)this[nameof(this.VaultsJsonFileLocation)]; }
            set { this[nameof(this.VaultsJsonFileLocation)] = value; }
        }

        [UserScopedSetting]
        [DefaultSettingValue(@"VaultAliases.json")]
        [DisplayName("Vault aliases file name")]
        [Description("Relative or absolute path to .json file with vault aliases.\nEnvironment variables are supported and expanded accordingly.")]
        [Category("Vaults configuration")]
        [Editor(typeof(FileNameEditor), typeof(UITypeEditor))]
        public string VaultAliasesJsonFileLocation
        {
            get { return (string)this[nameof(this.VaultAliasesJsonFileLocation)]; }
            set { this[nameof(this.VaultAliasesJsonFileLocation)] = value; }
        }

        [UserScopedSetting]
        [DefaultSettingValue(@"SecretKinds.json")]
        [DisplayName("Secret kinds file name")]
        [Description("Relative or absolute path to .json file with secret kinds definitions.\nEnvironment variables are supported and expanded accordingly.")]
        [Category("Vaults configuration")]
        [Editor(typeof(FileNameEditor), typeof(UITypeEditor))]
        public string SecretKindsJsonFileLocation
        {
            get { return (string)this[nameof(this.SecretKindsJsonFileLocation)]; }
            set { this[nameof(this.SecretKindsJsonFileLocation)] = value; }
        }

        [UserScopedSetting]
        [DefaultSettingValue(@"CustomTags.json")]
        [DisplayName("Custom tags file name")]
        [Description("Relative or absolute path to .json file with custom tags definitions.\nEnvironment variables are supported and expanded accordingly.")]
        [Category("Vaults configuration")]
        [Editor(typeof(FileNameEditor), typeof(UITypeEditor))]
        public string CustomTagsJsonFileLocation
        {
            get { return (string)this[nameof(this.CustomTagsJsonFileLocation)]; }
            set { this[nameof(this.CustomTagsJsonFileLocation)] = value; }
        }

        [UserScopedSetting]
        [DisplayName("User Account Names")]
        [Description("JSON array of saved user accounts used in the subscriptions manager dialog.")]
        [Category("Subscriptions dialog")]
        [Editor(typeof(MultilineStringEditor), typeof(UITypeEditor))]
        public string UserAccountNames
        {
            get { return (string)this[nameof(this.UserAccountNames)]; }
            set { this[nameof(this.UserAccountNames)] = value; }
        }

        [UserScopedSetting]
        [DefaultSettingValue("True")]
        [Browsable(false)]
        public bool UpgradeRequired
        {
            get { return (bool)this[nameof(this.UpgradeRequired)]; }
            set { this[nameof(this.UpgradeRequired)] = value; }
        }

        [UserScopedSetting]
        [DefaultSettingValue("")]
        [Browsable(false)]
        public string LastSelectedVaultAlias
        {
            get { return (string)this[nameof(this.LastSelectedVaultAlias)]; }
            set { this[nameof(this.LastSelectedVaultAlias)] = value; }
        }

        [Browsable(false)]
        public IEnumerable<string> UserAccountNamesList
        {
            get { return this.GetSavedUserAccounts().Select(a => a.AccountName); }
        }

        [UserScopedSetting]
        [DefaultSettingValue(@"{}")]
        [Browsable(false)]
        public string FavoriteSecretsJson
        {
            get { return (string)this[nameof(this.FavoriteSecretsJson)]; }
        }

        [Browsable(false)]
        public FavoriteSecretsDictionary FavoriteSecretsDictionary
        {
            get { return this._favoriteSecretsDictionary; }
        }

        public override void Save()
        {
            // new lines and spaces so user.config will look pretty
            this[nameof(this.FavoriteSecretsJson)] = "\n" + JsonConvert.SerializeObject(this._favoriteSecretsDictionary, Formatting.Indented) + "\n                ";
            base.Save();
        }

        public bool AddUserAccountName(string userAccountName)
        {
            string normalizedAccount = NormalizeAccountName(userAccountName);
            if (string.IsNullOrWhiteSpace(normalizedAccount))
            {
                return false;
            }

            if (this.GetSavedUserAccounts().Any(a => string.Equals(a.AccountName, normalizedAccount, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            this.AddOrUpdateSavedUserAccount(normalizedAccount, null, null);
            return true;
        }

        public bool AddUserAccountName(string userAccountName, string tenantId)
        {
            string normalizedAccount = NormalizeAccountName(userAccountName);
            if (string.IsNullOrWhiteSpace(normalizedAccount))
            {
                return false;
            }

            bool existed = this.GetSavedUserAccounts().Any(a => string.Equals(a.AccountName, normalizedAccount, StringComparison.OrdinalIgnoreCase));
            this.AddOrUpdateSavedUserAccount(normalizedAccount, tenantId, null);
            return !existed;
        }

        public void AddOrUpdateUserAccountName(string userAccountName, string tenantId)
        {
            this.AddOrUpdateSavedUserAccount(userAccountName, tenantId, null);
        }

        public IReadOnlyList<SavedUserAccount> GetSavedUserAccounts()
        {
            return this.ParseSavedUserAccounts().AsReadOnly();
        }

        public SavedUserAccount GetSavedUserAccount(string accountName)
        {
            string normalizedAccount = NormalizeAccountName(accountName);
            if (string.IsNullOrWhiteSpace(normalizedAccount))
            {
                return null;
            }

            return this.ParseSavedUserAccounts()
                .FirstOrDefault(a => string.Equals(a.AccountName, normalizedAccount, StringComparison.OrdinalIgnoreCase));
        }

        public void AddOrUpdateSavedUserAccount(string accountName, string defaultTenantId, IEnumerable<SavedTenantInfo> knownTenants)
        {
            string normalizedAccount = NormalizeAccountName(accountName);
            if (string.IsNullOrWhiteSpace(normalizedAccount))
            {
                return;
            }

            List<SavedUserAccount> accounts = this.ParseSavedUserAccounts();
            SavedUserAccount existing = accounts.FirstOrDefault(a => string.Equals(a.AccountName, normalizedAccount, StringComparison.OrdinalIgnoreCase));
            if (existing == null)
            {
                existing = new SavedUserAccount { AccountName = normalizedAccount };
                accounts.Add(existing);
            }

            string normalizedTenant = NormalizeTenantId(defaultTenantId);
            if (!string.IsNullOrWhiteSpace(normalizedTenant))
            {
                existing.DefaultTenantId = normalizedTenant;
            }

            if (knownTenants != null)
            {
                foreach (SavedTenantInfo tenant in knownTenants)
                {
                    string tenantId = NormalizeTenantId(tenant?.TenantId);
                    if (string.IsNullOrWhiteSpace(tenantId))
                    {
                        continue;
                    }

                    SavedTenantInfo existingTenant = existing.KnownTenants.FirstOrDefault(t => string.Equals(t.TenantId, tenantId, StringComparison.OrdinalIgnoreCase));
                    if (existingTenant == null)
                    {
                        existing.KnownTenants.Add(new SavedTenantInfo { TenantId = tenantId, DisplayName = tenant.DisplayName?.Trim() });
                    }
                    else if (!string.IsNullOrWhiteSpace(tenant.DisplayName))
                    {
                        existingTenant.DisplayName = tenant.DisplayName.Trim();
                    }
                }
            }

            this.PersistSavedUserAccounts(accounts);
        }

        public void SetDefaultTenantForAccount(string accountName, string tenantId)
        {
            string normalizedAccount = NormalizeAccountName(accountName);
            string normalizedTenant = NormalizeTenantId(tenantId);
            if (string.IsNullOrWhiteSpace(normalizedAccount) || string.IsNullOrWhiteSpace(normalizedTenant))
            {
                return;
            }

            this.AddOrUpdateSavedUserAccount(
                normalizedAccount,
                normalizedTenant,
                new[] { new SavedTenantInfo { TenantId = normalizedTenant } });
        }

        private List<SavedUserAccount> ParseSavedUserAccounts()
        {
            string raw = this.UserAccountNames?.Trim();
            if (string.IsNullOrWhiteSpace(raw))
            {
                return new List<SavedUserAccount>();
            }

            List<SavedUserAccount> fromJson = this.TryParseJsonSavedAccounts(raw);
            if (fromJson != null)
            {
                return NormalizeSavedAccounts(fromJson);
            }

            return NormalizeSavedAccounts(ParseLegacyAccounts(raw));
        }

        private List<SavedUserAccount> TryParseJsonSavedAccounts(string raw)
        {
            if (!raw.StartsWith("[", StringComparison.Ordinal))
            {
                return null;
            }

            try
            {
                JArray array = JArray.Parse(raw);
                if (array.Count == 0)
                {
                    return new List<SavedUserAccount>();
                }

                if (array.First.Type == JTokenType.String)
                {
                    return ParseLegacyAccounts(string.Join("\n", array.Values<string>()));
                }

                return array.ToObject<List<SavedUserAccount>>() ?? new List<SavedUserAccount>();
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static List<SavedUserAccount> ParseLegacyAccounts(string raw)
        {
            var results = new List<SavedUserAccount>();
            foreach (string line in raw.Split('\n').Select(s => s.Trim()).Where(s => !string.IsNullOrWhiteSpace(s)))
            {
                string[] accountAndTenant = line.Split('|');
                string accountName = NormalizeAccountName(accountAndTenant[0]);
                if (string.IsNullOrWhiteSpace(accountName))
                {
                    continue;
                }

                string tenantId = accountAndTenant.Length > 1 ? NormalizeTenantId(accountAndTenant[1]) : null;
                results.Add(new SavedUserAccount
                {
                    AccountName = accountName,
                    DefaultTenantId = tenantId,
                    KnownTenants = string.IsNullOrWhiteSpace(tenantId)
                        ? new List<SavedTenantInfo>()
                        : new List<SavedTenantInfo> { new SavedTenantInfo { TenantId = tenantId } },
                });
            }

            return results;
        }

        private static List<SavedUserAccount> NormalizeSavedAccounts(List<SavedUserAccount> accounts)
        {
            var merged = new Dictionary<string, SavedUserAccount>(StringComparer.OrdinalIgnoreCase);
            foreach (SavedUserAccount account in accounts ?? new List<SavedUserAccount>())
            {
                string accountName = NormalizeAccountName(account?.AccountName);
                if (string.IsNullOrWhiteSpace(accountName))
                {
                    continue;
                }

                if (!merged.TryGetValue(accountName, out SavedUserAccount existing))
                {
                    existing = new SavedUserAccount
                    {
                        AccountName = accountName,
                        DefaultTenantId = NormalizeTenantId(account.DefaultTenantId),
                        KnownTenants = new List<SavedTenantInfo>(),
                    };
                    merged[accountName] = existing;
                }

                if (string.IsNullOrWhiteSpace(existing.DefaultTenantId))
                {
                    existing.DefaultTenantId = NormalizeTenantId(account.DefaultTenantId);
                }

                foreach (SavedTenantInfo tenant in account.KnownTenants ?? new List<SavedTenantInfo>())
                {
                    string tenantId = NormalizeTenantId(tenant?.TenantId);
                    if (string.IsNullOrWhiteSpace(tenantId))
                    {
                        continue;
                    }

                    SavedTenantInfo existingTenant = existing.KnownTenants.FirstOrDefault(t => string.Equals(t.TenantId, tenantId, StringComparison.OrdinalIgnoreCase));
                    if (existingTenant == null)
                    {
                        existing.KnownTenants.Add(new SavedTenantInfo { TenantId = tenantId, DisplayName = tenant.DisplayName?.Trim() });
                    }
                    else if (string.IsNullOrWhiteSpace(existingTenant.DisplayName) && !string.IsNullOrWhiteSpace(tenant.DisplayName))
                    {
                        existingTenant.DisplayName = tenant.DisplayName.Trim();
                    }
                }
            }

            return merged.Values.OrderBy(a => a.AccountName, StringComparer.OrdinalIgnoreCase).ToList();
        }

        private void PersistSavedUserAccounts(List<SavedUserAccount> accounts)
        {
            List<SavedUserAccount> normalized = NormalizeSavedAccounts(accounts);
            this[nameof(this.UserAccountNames)] = JsonConvert.SerializeObject(normalized, Formatting.Indented);
            base.Save();
        }

        private static string NormalizeAccountName(string accountName)
        {
            return string.IsNullOrWhiteSpace(accountName) ? null : accountName.Trim();
        }

        private static string NormalizeTenantId(string tenantId)
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
