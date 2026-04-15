// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Vault.Explorer.Services;
using Microsoft.Vault.Explorer.ViewModels;
using Microsoft.Vault.Explorer.Views;

namespace Microsoft.Vault.Explorer;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();

        // Apply saved theme (accent colors + light/dark variant) before window opens
        ApplyTheme(AppSettings.Default.Theme);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = Services.GetRequiredService<MainWindowViewModel>()
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// Swaps the active theme ResourceDictionary and sets the Fluent light/dark base.
    /// Safe to call at any time (including from SettingsViewModel for live preview).
    /// </summary>
    public static void ApplyTheme(string themeName)
    {
        if (Current == null) return;

        var fileName = themeName switch
        {
            "Ocean Depths"       => "OceanDepths",
            "Modern Minimalist"  => "ModernMinimalist",
            "Arctic Frost"       => "ArcticFrost",
            "Midnight Galaxy"    => "MidnightGalaxy",
            _                    => "OceanDepths"
        };

        var uri = new Uri($"avares://VaultExplorer/Themes/{fileName}.axaml");
        var themeDict = (Avalonia.Controls.ResourceDictionary)AvaloniaXamlLoader.Load(uri);

        // Remove any previously applied vault theme dict (identified by sentinel key)
        var merged = Current.Resources.MergedDictionaries;
        var old = merged.OfType<Avalonia.Controls.ResourceDictionary>()
                        .Where(d => d.ContainsKey("__VaultTheme__"))
                        .ToList();
        foreach (var d in old) merged.Remove(d);
        merged.Add(themeDict);

        // Set Fluent light/dark base to match theme
        Current.RequestedThemeVariant = themeName is "Modern Minimalist" or "Arctic Frost"
            ? ThemeVariant.Light
            : ThemeVariant.Dark;
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // ── Data protection (cross-platform secret file encryption) ───────────
        var keysDir = new DirectoryInfo(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AzureKeyVaultExplorer", "DataProtection-Keys"));

        var dpBuilder = services.AddDataProtection()
            .SetApplicationName("AzureKeyVaultExplorer")
            .PersistKeysToFileSystem(keysDir);

        // On Windows: additionally protect the key ring with current-user DPAPI
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            dpBuilder.ProtectKeysWithDpapi();

        // Register a named protector for KeyVaultFile use
        services.AddSingleton<IDataProtector>(sp =>
            sp.GetRequiredService<IDataProtectionProvider>()
              .CreateProtector("KeyVaultFile.v1"));

        // ── Infrastructure services ───────────────────────────────────────────
        services.AddSingleton<IDialogService, AvaloniaDialogService>();
        services.AddSingleton<IClipboardService, AvaloniaClipboardService>();
        services.AddSingleton<INotificationService, AvaloniaNotificationService>();
        services.AddSingleton<ICertificatePickerService, AvaloniaCertificatePickerService>();
        services.AddSingleton<IIdleDetectionService, AvaloniaIdleDetectionService>();
        services.AddSingleton<IProtocolHandlerService>(ProtocolHandlerServiceFactory.Create());

        // ── ViewModels ────────────────────────────────────────────────────────
        services.AddSingleton<MainWindowViewModel>();
    }
}
