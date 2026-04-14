// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using Microsoft.Vault.Explorer.Controls.Lists.Favorites;
using Newtonsoft.Json;

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

    // ── General ──────────────────────────────────────────────────────────────

    [JsonProperty]
    public TimeSpan CopyToClipboardTimeToLive { get; set; } = TimeSpan.FromSeconds(30);

    [JsonProperty]
    public TimeSpan AboutToExpireWarningPeriod { get; set; } = TimeSpan.FromDays(14);

    /// <summary>Named color string for items about to expire (e.g. "Orange").</summary>
    [JsonProperty]
    public string AboutToExpireItemColor { get; set; } = "Orange";

    /// <summary>Named color string for expired / not-yet-active items (e.g. "Red").</summary>
    [JsonProperty]
    public string ExpiredItemColor { get; set; } = "Red";

    /// <summary>Named color string for disabled items (e.g. "DarkGray").</summary>
    [JsonProperty]
    public string DisabledItemColor { get; set; } = "DarkGray";

    // ── Vaults configuration ─────────────────────────────────────────────────

    [JsonProperty]
    public string JsonConfigurationFilesRoot { get; set; } = "";

    [JsonProperty]
    public string VaultsJsonFileLocation { get; set; } = "Vaults.json";

    [JsonProperty]
    public string VaultAliasesJsonFileLocation { get; set; } = "VaultAliases.json";

    [JsonProperty]
    public string SecretKindsJsonFileLocation { get; set; } = "SecretKinds.json";

    [JsonProperty]
    public string CustomTagsJsonFileLocation { get; set; } = "CustomTags.json";

    // ── Subscriptions dialog ─────────────────────────────────────────────────

    /// <summary>Newline-separated list of user account names shown in the subscriptions dialog.</summary>
    [JsonProperty]
    public string UserAccountNames { get; set; } = "";

    // ── Internal / non-browsable ─────────────────────────────────────────────

    [JsonProperty]
    public string LastUsedVaultAlias { get; set; } = "";

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
}
