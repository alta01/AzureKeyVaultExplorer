// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using Microsoft.Vault.Explorer.Controls.Lists.Favorites;
using Newtonsoft.Json;
using Avalonia.Styling;

namespace Microsoft.Vault.Explorer;

/// <summary>
/// JSON-backed user settings.
/// Replaces System.Configuration.ApplicationSettingsBase (Windows-only user.config).
/// Stored at: {SpecialFolder.ApplicationData}/VaultExplorerNext/settings.json
/// </summary>
public sealed class AppSettings : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private static AppSettings? _instance;
    private static readonly object _lock = new();

    public static AppSettings Default
    {
        get
        {
            if (_instance is null)
                lock (_lock)
                    _instance ??= Load();
            return _instance;
        }
    }

    // ── Appearance ───────────────────────────────────────────────────────────

    [Category("Appearance")]
    [DisplayName("Theme")]
    [Description("Color theme: 'Dark' or 'Light'. Takes effect immediately when changed in Settings.")]
    [JsonProperty]
    public string Theme { get; set; } = "Dark";

    // ── General ──────────────────────────────────────────────────────────────

    [Category("General")]
    [DisplayName("Clipboard Clear Delay")]
    [Description("How long a copied secret value stays in the clipboard before being automatically cleared. Default: 30 seconds. Format: hh:mm:ss")]
    [JsonProperty]
    public TimeSpan CopyToClipboardTimeToLive { get; set; } = TimeSpan.FromSeconds(30);

    [Category("General")]
    [DisplayName("Expiry Warning Period")]
    [Description("How far in advance an item is highlighted as 'about to expire'. Default: 14 days. Format: d.hh:mm:ss")]
    [JsonProperty]
    public TimeSpan AboutToExpireWarningPeriod { get; set; } = TimeSpan.FromDays(14);

    [Category("General")]
    [DisplayName("Expiring Item Color")]
    [Description("Row highlight color for secrets/certificates that will expire within the warning period. Examples: Orange, Yellow, Gold.")]
    [JsonProperty]
    public string AboutToExpireItemColor { get; set; } = "Orange";

    [Category("General")]
    [DisplayName("Expired Item Color")]
    [Description("Row highlight color for secrets/certificates that have already expired or are not yet active. Examples: Red, Crimson.")]
    [JsonProperty]
    public string ExpiredItemColor { get; set; } = "Red";

    [Category("General")]
    [DisplayName("Disabled Item Color")]
    [Description("Row highlight color for secrets/certificates that have been explicitly disabled in Key Vault. Examples: DarkGray, Gray.")]
    [JsonProperty]
    public string DisabledItemColor { get; set; } = "DarkGray";

    // ── Vault configuration files ────────────────────────────────────────────

    [Category("Vault Configuration")]
    [DisplayName("Config Files Root Folder")]
    [Description("Folder that contains all vault configuration JSON files (Vaults.json, VaultAliases.json, etc.). Leave empty to use the default app data folder. Open Settings Folder in About tab to locate it.")]
    [JsonProperty]
    public string JsonConfigurationFilesRoot { get; set; } = "";

    [Category("Vault Configuration")]
    [DisplayName("Vaults File")]
    [Description("File name (relative to Config Root) for the Vaults.json file that defines Azure Key Vault connection details: vault name, subscription, resource group, and tenant. Default: Vaults.json")]
    [JsonProperty]
    public string VaultsJsonFileLocation { get; set; } = "Vaults.json";

    [Category("Vault Configuration")]
    [DisplayName("Vault Aliases File")]
    [Description("File name (relative to Config Root) for VaultAliases.json. Each entry in this file creates a named shortcut in the Vault dropdown on the main window. Add entries here to add new vaults. Default: VaultAliases.json")]
    [JsonProperty]
    public string VaultAliasesJsonFileLocation { get; set; } = "VaultAliases.json";

    [Category("Vault Configuration")]
    [DisplayName("Secret Kinds File")]
    [Description("File name (relative to Config Root) for SecretKinds.json. Defines custom secret type templates that appear in the secret editor dropdown. Default: SecretKinds.json")]
    [JsonProperty]
    public string SecretKindsJsonFileLocation { get; set; } = "SecretKinds.json";

    [Category("Vault Configuration")]
    [DisplayName("Custom Tags File")]
    [Description("File name (relative to Config Root) for CustomTags.json. Defines suggested tag key/value pairs shown in the tag editor. Default: CustomTags.json")]
    [JsonProperty]
    public string CustomTagsJsonFileLocation { get; set; } = "CustomTags.json";

    // ── Accounts ─────────────────────────────────────────────────────────────

    [Category("Accounts")]
    [DisplayName("Azure Account Names")]
    [Description("One Azure account per line (e.g. user@contoso.com). These are shown in the Subscriptions Manager when picking a vault from a subscription. Add your account here before using 'Pick vault from subscription'.")]
    [JsonProperty]
    public string UserAccountNames { get; set; } = "";

    // ── Internal / non-browsable ─────────────────────────────────────────────

    [Browsable(false)]
    [JsonProperty]
    public string LastUsedVaultAlias { get; set; } = "";

    [Browsable(false)]
    [JsonProperty]
    public FavoriteSecretsDictionary FavoriteSecretsDictionary { get; set; } = new();

    // ── Derived helpers ──────────────────────────────────────────────────────

    [JsonIgnore]
    public IEnumerable<string> UserAccountNamesList =>
        from s in UserAccountNames.Split('\n')
        where !string.IsNullOrWhiteSpace(s)
        select s.Trim();

    // ── Persistence ──────────────────────────────────────────────────────────

    [JsonIgnore]
    public static string SettingsFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "VaultExplorerNext",
        "settings.json");

    private static AppSettings Load()
    {
        if (File.Exists(SettingsFilePath))
        {
            try
            {
                var json = File.ReadAllText(SettingsFilePath);
                return JsonConvert.DeserializeObject<AppSettings>(json) ?? new AppSettings();
            }
            catch
            {
                // Corrupt settings — start fresh.
            }
        }

        return new AppSettings();
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsFilePath)!);
        var json = JsonConvert.SerializeObject(this, Formatting.Indented);
        File.WriteAllText(SettingsFilePath, json);
    }

    public bool AddUserAccountName(string userAccountName)
    {
        if (!UserAccountNames.Contains(userAccountName))
        {
            UserAccountNames += "\n" + userAccountName;
            Save();
            return true;
        }

        return false;
    }

    /// <summary>Copies all property values from <paramref name="src"/> into this instance.</summary>
    public void CopyFrom(AppSettings src)
    {
        Theme = src.Theme;
        CopyToClipboardTimeToLive = src.CopyToClipboardTimeToLive;
        AboutToExpireWarningPeriod = src.AboutToExpireWarningPeriod;
        AboutToExpireItemColor = src.AboutToExpireItemColor;
        ExpiredItemColor = src.ExpiredItemColor;
        DisabledItemColor = src.DisabledItemColor;
        JsonConfigurationFilesRoot = src.JsonConfigurationFilesRoot;
        VaultsJsonFileLocation = src.VaultsJsonFileLocation;
        VaultAliasesJsonFileLocation = src.VaultAliasesJsonFileLocation;
        SecretKindsJsonFileLocation = src.SecretKindsJsonFileLocation;
        CustomTagsJsonFileLocation = src.CustomTagsJsonFileLocation;
        UserAccountNames = src.UserAccountNames;
        LastUsedVaultAlias = src.LastUsedVaultAlias;
    }

    /// <summary>Converts Theme string to Avalonia ThemeVariant.</summary>
    [Browsable(false)]
    [JsonIgnore]
    public ThemeVariant ThemeVariant =>
        string.Equals(Theme, "Light", StringComparison.OrdinalIgnoreCase)
            ? ThemeVariant.Light
            : ThemeVariant.Dark;
}
