// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.IO;
using Avalonia;
using Avalonia.ReactiveUI;
using Microsoft.Vault.Library;

namespace Microsoft.Vault.Explorer;

internal static class Program
{
    // Avalonia requires [STAThread] on desktop.
    [STAThread]
    public static void Main(string[] args)
    {
        // Ensure config directory exists and populate default files on first run.
        SetupConfigurationFiles();

        // Initialise default user name used when constructing vault access tokens.
        Globals.DefaultUserName =
            AppSettings.Default.UserAccountNamesList.Count > 0
                ? AppSettings.Default.UserAccountNamesList[0]
                : Environment.UserName;

        // Register vault:// protocol handler (Windows = registry, macOS = plist helper,
        // Linux = xdg .desktop file — each handled inside the service implementation).
        try { ActivationUri.RegisterVaultProtocol(); }
        catch { /* Non-fatal — protocol handler is a convenience feature */ }

        // Clear all cached auth tokens on clean exit.
        AppDomain.CurrentDomain.ProcessExit += (_, _) => FileTokenCache.ClearAllFileTokenCaches();

        // Unhandled exception fallback — log and exit gracefully.
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
                Console.Error.WriteLine($"Unhandled exception: {ex}");
        };

        // Assembly redirect: old Microsoft.PS.Common.Vault → new Microsoft.Vault.Library
        AppDomain.CurrentDomain.AssemblyResolve += (_, args) =>
        {
            if (args.Name == "Microsoft.PS.Common.Vault")
                foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
                    if (a.GetName().Name == "Microsoft.Vault.Library") return a;
            return null;
        };

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    /// <summary>
    /// Avalonia app builder — also used by the Avalonia designer.
    /// </summary>
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .UseReactiveUI()
            .LogToTrace();

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void SetupConfigurationFiles()
    {
        try
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string configDir = Path.Combine(localAppData, Globals.ProductName, "Config");

            if (!Directory.Exists(configDir))
                Directory.CreateDirectory(configDir);

            string[] configFiles = { "Vaults.json", "VaultAliases.json", "SecretKinds.json", "CustomTags.json" };
            string appDir = Path.Combine(AppContext.BaseDirectory, "Config", "Templates");

            bool anyFileCopied = false;
            foreach (string configFile in configFiles)
            {
                string src = Path.Combine(appDir, configFile);
                string dst = Path.Combine(configDir, configFile);
                if (File.Exists(src) && !File.Exists(dst))
                {
                    File.Copy(src, dst);
                    anyFileCopied = true;
                }
            }

            var settings = AppSettings.Default;
            bool usingDefault = string.IsNullOrEmpty(settings.JsonConfigurationFilesRoot)
                || string.Equals(settings.JsonConfigurationFilesRoot, configDir,
                    StringComparison.OrdinalIgnoreCase);

            if (usingDefault && (anyFileCopied || Directory.GetFiles(configDir, "*.json").Length > 0))
            {
                settings.JsonConfigurationFilesRoot = configDir;
                settings.Save();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SetupConfigurationFiles: {ex.Message}");
        }
    }
}
