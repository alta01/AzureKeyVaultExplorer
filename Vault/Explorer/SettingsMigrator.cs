// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.IO;

namespace Microsoft.Vault.Explorer;

/// <summary>
/// One-time migration to ensure AppSettings JSON is initialised on first run.
/// The legacy ApplicationSettingsBase (user.config) source was removed in Phase 5.
/// </summary>
public static class SettingsMigrator
{
    /// <summary>
    /// Ensures AppSettings are persisted. Safe to call on every startup.
    /// </summary>
    public static void MigrateIfNeeded()
    {
        if (File.Exists(AppSettings.SettingsFilePath))
            return; // Already initialised.

        AppSettings.Default.Save();
    }
}
