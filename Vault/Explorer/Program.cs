// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.ReactiveUI;
using Microsoft.Vault.Explorer.Common;
using Microsoft.Vault.Library;

namespace Microsoft.Vault.Explorer;

internal static class Program
{
    // Log file that survives app exit — readable by Claude after a crash.
    internal static readonly string CrashLogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AzureKeyVaultExplorer", "crash.log");

    // Avalonia requires [STAThread] on desktop.
    [STAThread]
    public static void Main(string[] args)
    {
        // Ensure crash log directory exists and clear previous log.
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CrashLogPath)!);
            File.WriteAllText(CrashLogPath, $"=== Session started {DateTime.Now:u} ===\n");
        }
        catch { }

        // Ensure config directory exists and populate default files on first run.
        SetupConfigurationFiles();

        // Initialise default user name used when constructing vault access tokens.
        Globals.DefaultUserName =
            AppSettings.Default.UserAccountNamesList.Any()
                ? AppSettings.Default.UserAccountNamesList.First()
                : Environment.UserName;

        // Register vault:// protocol handler (Windows = registry, macOS = plist helper,
        // Linux = xdg .desktop file — each handled inside the service implementation).
        try { ActivationUri.RegisterVaultProtocol(); }
        catch { /* Non-fatal — protocol handler is a convenience feature */ }

        // Clear all cached auth tokens on clean exit.
        AppDomain.CurrentDomain.ProcessExit += (_, _) => FileTokenCache.ClearAllFileTokenCaches();

        // Unhandled exception fallback — write to crash log and stderr.
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
            {
                string msg = $"[{DateTime.Now:u}] Unhandled exception:\n{ex}\n";
                Console.Error.WriteLine(msg);
                try { File.AppendAllText(CrashLogPath, msg); } catch { }
            }
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
