// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
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

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = Services.GetRequiredService<MainWindowViewModel>()
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
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
