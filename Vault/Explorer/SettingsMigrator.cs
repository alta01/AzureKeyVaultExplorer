// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.IO;
using System.Drawing;

namespace Microsoft.Vault.Explorer;

/// <summary>
/// One-time migration from the legacy ApplicationSettingsBase user.config to AppSettings JSON.
/// Run on application startup if settings.json does not yet exist but a user.config does.
/// </summary>
public static class SettingsMigrator
{
    /// <summary>
    /// Checks whether migration is needed and performs it if so.
    /// Safe to call on every startup — no-ops if already migrated.
    /// </summary>
    public static void MigrateIfNeeded()
    {
        if (File.Exists(AppSettings.SettingsFilePath))
            return; // Already migrated.

        // On non-Windows there is no user.config to migrate from.
        if (!OperatingSystem.IsWindows())
        {
            AppSettings.Default.Save();
            return;
        }

        TryMigrateFromUserConfig();
    }

    private static void TryMigrateFromUserConfig()
    {
        try
        {
            // Load legacy settings via ApplicationSettingsBase so we get the exact user.config values.
            var legacy = new Settings();

            var target = AppSettings.Default;
            target.CopyToClipboardTimeToLive = legacy.CopyToClipboardTimeToLive;
            target.AboutToExpireWarningPeriod = legacy.AboutToExpireWarningPeriod;
            target.AboutToExpireItemColor = ColorToName(legacy.AboutToExpireItemColor);
            target.ExpiredItemColor = ColorToName(legacy.ExpiredItemColor);
            target.DisabledItemColor = ColorToName(legacy.DisabledItemColor);
            target.JsonConfigurationFilesRoot = legacy.JsonConfigurationFilesRoot ?? "";
            target.VaultsJsonFileLocation = legacy.VaultsJsonFileLocation;
            target.VaultAliasesJsonFileLocation = legacy.VaultAliasesJsonFileLocation;
            target.SecretKindsJsonFileLocation = legacy.SecretKindsJsonFileLocation;
            target.CustomTagsJsonFileLocation = legacy.CustomTagsJsonFileLocation;
            target.UserAccountNames = legacy.UserAccountNames ?? "";
            target.LastUsedVaultAlias = legacy.LastUsedVaultAlias ?? "";
            target.FavoriteSecretsDictionary = legacy.FavoriteSecretsDictionary;
            target.Save();
        }
        catch
        {
            // If anything goes wrong, just use defaults.
            AppSettings.Default.Save();
        }
    }

    private static string ColorToName(Color c)
    {
        // For known named colors return the name, otherwise return the hex value.
        if (c.IsNamedColor)
            return c.Name;
        return $"#{c.R:X2}{c.G:X2}{c.B:X2}";
    }
}
