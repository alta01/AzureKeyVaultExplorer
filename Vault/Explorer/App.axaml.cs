using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Vault.Explorer.Services;
using Microsoft.Vault.Explorer.ViewModels;
using Microsoft.Vault.Explorer.Views;

namespace Microsoft.Vault.Explorer;

// Phase 0 scaffold — not yet wired as entry point.
// Phase 1: register all IService implementations here.
// Phase 4: create MainWindow and wire MainWindowViewModel.
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
            // Phase 4: replace the line below with MainWindow creation.
            // desktop.MainWindow = new MainWindow
            // {
            //     DataContext = Services.GetRequiredService<MainWindowViewModel>()
            // };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Phase 1: register service implementations here.
        // services.AddSingleton<INotificationService, AvaloniaNotificationService>();
        // services.AddSingleton<ICertificatePickerService, AvaloniaCertificatePickerService>();
        // services.AddSingleton<IClipboardService, AvaloniaClipboardService>();
        // services.AddSingleton<IDialogService, AvaloniaDialogService>();
        // services.AddSingleton<IIdleDetectionService, AvaloniaIdleDetectionService>();
        // services.AddSingleton<IProtocolHandlerService>(ProtocolHandlerServiceFactory.Create());
        // services.AddSingleton<AppSettings>();
        //
        // Phase 2: register ViewModels here.
        // services.AddTransient<MainWindowViewModel>();
        // services.AddTransient<VaultListViewModel>();
    }
}
