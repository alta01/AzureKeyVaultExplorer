# AGENTS.md — Azure Key Vault Explorer

This file provides context for AI coding agents working in this repository.

## What this project is

Azure Key Vault Explorer is a **cross-platform** desktop app (net10.0 / Avalonia UI 11) for
browsing, editing, and managing secrets, keys, and certificates in Azure Key Vault.

This repository is a fork of [reysic/AzureKeyVaultExplorer](https://github.com/reysic/AzureKeyVaultExplorer),
which originated from [microsoft/AzureKeyVaultExplorer](https://github.com/microsoft/AzureKeyVaultExplorer).
The original app was Windows Forms only; this fork has been fully migrated to Avalonia UI so it
runs on Windows, macOS, and Linux.

## Tech stack

| Layer | Technology |
|-------|-----------|
| UI framework | Avalonia UI 11.3.x |
| MVVM | ReactiveUI + DynamicData (`SourceList<T>`) |
| Icons | Material.Icons.Avalonia 2.1.0 |
| Auth | Microsoft.Identity.Client (MSAL v4) — browser-based OAuth |
| Azure (data plane) | Azure.Security.KeyVault.Secrets 4.7, Azure.Security.KeyVault.Certificates 4.7 |
| Azure (management) | Azure.ResourceManager 1.14, Azure.ResourceManager.KeyVault 1.3 |
| Settings | JSON in `SpecialFolder.ApplicationData/VaultExplorerNext/` |
| Secrets encryption | Microsoft.AspNetCore.DataProtection (cross-platform DPAPI replacement) |
| Target framework | `net10.0` |

## Repository layout

```
AzureKeyVaultExplorer.sln
Vault/
  Explorer/                        Main Avalonia app (VaultExplorer.csproj)
    App.axaml / App.axaml.cs       Avalonia application + DI container
    Program.cs                     Entry point — BuildAvaloniaApp / Desktop lifetime
    AppSettings.cs                 JSON-backed user settings
    Themes/                        ResourceDictionary theme files (4 themes)
    Views/                         .axaml views (no business logic)
      MainWindow.axaml             Main window: menu, toolbar, vault tabs, status bar
      Dialogs/
        SecretDialogView.axaml
        CertificateDialogView.axaml
        SubscriptionsManagerView.axaml
        SettingsView.axaml
        MessageDialogView.axaml
        ExceptionDialogView.axaml
        PasswordDialogView.axaml
    ViewModels/                    One ViewModel per View (ReactiveObject subclasses)
      ViewModelBase.cs
      MainWindowViewModel.cs       ISession impl; Tabs collection; all toolbar commands
      VaultTabViewModel.cs         One tab = one open vault
      VaultListViewModel.cs        DynamicData filtered/sorted list
      VaultItemViewModel.cs        Base for secret/cert rows
      VaultSecretViewModel.cs
      VaultCertificateViewModel.cs
      SettingsViewModel.cs
      SubscriptionsManagerViewModel.cs
      SecretDialogViewModel.cs
      CertificateDialogViewModel.cs
    Services/                      Platform-abstracted services (registered in DI)
      IDialogService.cs + AvaloniaDialogService.cs
      IClipboardService.cs + AvaloniaClipboardService.cs
      INotificationService.cs + AvaloniaNotificationService.cs
      ICertificatePickerService.cs + AvaloniaCertificatePickerService.cs
      IIdleDetectionService.cs + AvaloniaIdleDetectionService.cs
      IProtocolHandlerService.cs + platform impls
    Common/                        Helpers: Globals, UxOperation, ActivationUri
    Model/                         Domain objects: PropObjects, ContentTypes, Tags, Aliases
  Library/                         Vault access abstractions (VaultLibrary.csproj)
    Vault.cs                       Core Vault class; ListSecretsAsync, GetSecretAsync, etc.
    VaultConfig.cs / VaultAccessType.cs  JSON-deserialized vault credential config
    Utils.cs                       GuardVaultName, GuardTagKey, GuardTagValue, etc.
  Core/                            Utilities shared across Library and Explorer
  ClearClipboard/                  Companion exe: clears clipboard after TTL expires
```

## How to build locally

```bash
dotnet build AzureKeyVaultExplorer.sln
```

Requirements: .NET 10 SDK (`dotnet --list-sdks` should show `10.x`). No Visual Studio required;
the project builds and runs on Linux, macOS, and Windows.

## MVVM conventions

### ViewModels

- Inherit `ViewModelBase` (extends `ReactiveObject`)
- Properties use `this.RaiseAndSetIfChanged(ref _field, value)`
- Commands use `ReactiveCommand.Create` / `CreateFromTask`
- No `System.Windows.Forms` or `System.Drawing` imports anywhere in ViewModels/Services
- Constructor injection only — services come from `App.Services` (Microsoft.Extensions.DI)

### Views (code-behind)

- Only wiring: `WhenActivated`, event routing to ViewModel methods, `FindControl`
- No business logic in code-behind
- Interactions (dialogs that need a window parent) are handled via `Interaction<TInput, TOutput>`
  defined on the ViewModel and subscribed in the View's `WhenActivated`

### Reactive filtering (DynamicData pattern)

```csharp
_source.Connect()
    .Filter(this.WhenAnyValue(x => x.SearchText)
        .Select(q => (Func<VaultItemViewModel, bool>)(item =>
            string.IsNullOrEmpty(q) || item.Name.Contains(q, StringComparison.OrdinalIgnoreCase))))
    .Sort(SortExpressionComparer<VaultItemViewModel>.Ascending(x => x.Name))
    .ObserveOn(RxApp.MainThreadScheduler)
    .Bind(out var items)
    .Subscribe()
    .DisposeWith(_disposables);
Items = items;
```

## Vault tab architecture

`MainWindowViewModel` owns `ObservableCollection<VaultTabViewModel> Tabs`. Each tab holds one
`VaultListViewModel` + the `VaultAlias` + `Library.Vault` for that session.

`VaultListViewModel VaultListViewModel => SelectedTab?.VaultList ?? _emptyVaultList;` is the
proxy property that AXAML binds to — always valid even when no tab is open.

Commands (`EditCommand`, `DeleteCommand`, etc.) route through `SelectedTab?.VaultList?.SelectedItem`.

## Service injection

Services are registered in `App.axaml.cs → ConfigureServices()`:

```csharp
services.AddSingleton<IDialogService, AvaloniaDialogService>();
services.AddSingleton<IClipboardService, AvaloniaClipboardService>();
// ... etc.
services.AddTransient<MainWindowViewModel>();
```

In ViewModels: receive via constructor injection.
In Views/code-behind (last resort): `App.Services.GetRequiredService<IDialogService>()`.

## Theme system

Four `ResourceDictionary` files in `Vault/Explorer/Themes/` override Avalonia Fluent theme keys.
`App.ApplyTheme(string name)` (in `App.axaml.cs`) merges the selected dictionary at runtime and
sets `RequestedThemeVariant` to `Light` or `Dark` accordingly.

Default theme: **Arctic Frost** (light). Changed in `AppSettings.cs`:
```csharp
public string Theme { get; set; } = "Arctic Frost";
```

## Security notes

- `TypeNameHandling` in `Vault.cs` JSON deserialization is set to `None`. Polymorphism for
  `VaultAccess` subtypes is handled per-property via `[JsonProperty(ItemTypeNameHandling = TypeNameHandling.Objects)]`.
- Tag keys and values are validated with `Utils.GuardTagKey` / `Utils.GuardTagValue` (256-char
  Azure limits; `microsoft` prefix rejected for keys).
- Settings path fields validate that values are absolute paths with no `..` traversal components.
- `GetSecretAsync` and `GetCertificateAsync` retry with exponential back-off on HTTP 429.

## Configuration files (user-editable)

Loaded from the path set in **Settings → JSON configuration files root** (default: app directory):

| File | Purpose |
|------|---------|
| `Vaults.json` | Vault credential definitions (optional if current account has access) |
| `VaultAliases.json` | Named vault aliases in the main dropdown |
| `SecretKinds.json` | Regex-validated secret types with tag schemas |
| `CustomTags.json` | Tag definitions referenced by SecretKinds |

## Coding conventions

- Namespace prefix: `Microsoft.Vault.*`
- `async void` only in Avalonia event handlers; wrap body in try-catch.
- All user-visible strings are inline (no resource file abstraction).
- No `*.Designer.cs` or `*.resx` files — all removed in the WinForms→Avalonia migration.
- AXAML `Kind` attributes for `MaterialIcon` inside `DataTemplate` must use
  `{x:Static materialIcons:MaterialIconKind.IconName}` — plain string values don't work in
  compiled DataTemplates.

## Known open items

- `vault://` protocol handler: Windows registry registration works; Linux `.desktop` MIME
  registration not yet implemented.
- Secret compression (gzip + base64 content type) is in `Library` but not yet wired to
  `SecretDialogViewModel`.
- PowerShell integration was removed (the upstream WinForms implementation doesn't port cleanly).
