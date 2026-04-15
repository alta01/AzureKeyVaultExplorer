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

    [Browsable(false)]
    [JsonProperty]
    public string Theme { get; set; } = "Ocean Depths";

    // ── General ──────────────────────────────────────────────────────────────

    [Browsable(false)]
    [JsonProperty]
    public TimeSpan CopyToClipboardTimeToLive { get; set; } = TimeSpan.FromMinutes(1);

    [Browsable(false)]
    [JsonProperty]
    public TimeSpan AboutToExpireWarningPeriod { get; set; } = TimeSpan.FromHours(24);

    [Browsable(false)]
    [JsonProperty]
    public string AboutToExpireItemColor { get; set; } = "Orange";

    [Browsable(false)]
    [JsonProperty]
    public string ExpiredItemColor { get; set; } = "Red";

    [Browsable(false)]
    [JsonProperty]
    public string DisabledItemColor { get; set; } = "DarkGray";

    // ── Vault configuration files ────────────────────────────────────────────

    [Browsable(false)]
    [JsonProperty]
    public string JsonConfigurationFilesRoot { get; set; } = "";

    [Browsable(false)]
    [JsonProperty]
    public string VaultsJsonFileLocation { get; set; } = "Vaults.json";

    [Browsable(false)]
    [JsonProperty]
    public string VaultAliasesJsonFileLocation { get; set; } = "VaultAliases.json";

    [Browsable(false)]
    [JsonProperty]
    public string SecretKindsJsonFileLocation { get; set; } = "SecretKinds.json";

    [Browsable(false)]
    [JsonProperty]
    public string CustomTagsJsonFileLocation { get; set; } = "CustomTags.json";

    // ── Accounts ─────────────────────────────────────────────────────────────

    [Browsable(false)]
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

    /// <summary>Converts Theme name to Avalonia ThemeVariant (light/dark base).</summary>
    [Browsable(false)]
    [JsonIgnore]
    public ThemeVariant ThemeVariant =>
        Theme is "Modern Minimalist" or "Arctic Frost"
            ? ThemeVariant.Light
            : ThemeVariant.Dark;
}
