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
| UI framework | Avalonia UI 11.3.8 |
| MVVM | ReactiveUI + DynamicData (`SourceCache<T, TKey>`) |
| Icons | Material.Icons.Avalonia 2.1.0 |
| Auth | Microsoft.Identity.Client (MSAL) 4.83.3 — browser-based OAuth |
| Azure (data plane) | Azure.Security.KeyVault.Secrets 4.7.0, Azure.Security.KeyVault.Certificates 4.7.0 |
| Azure (management) | Azure.ResourceManager 1.14.0, Azure.ResourceManager.KeyVault 1.3.3 |
| Azure (credential) | Azure.Identity 1.13.1 (AzureCliCredential + ManagedIdentityCredential chain in `VaultAccessTokenCredential`) |
| DI container | Microsoft.Extensions.DependencyInjection 10.0.0 |
| Settings | JSON in `SpecialFolder.ApplicationData/VaultExplorerNext/` |
| Secrets encryption | Microsoft.AspNetCore.DataProtection 10.0.0 (cross-platform DPAPI replacement) |
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

`VaultListViewModel` uses `SourceCache<VaultItemViewModel, string>` (keyed by `Name`), not
`SourceList<T>`. The full reactive chain:

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

> Note: `.Sort()` is obsolete in DynamicData 9.4+; replacement is `.SortAndBind()`. Both work;
> the current code uses `.Sort()` followed by `.Bind()` (tracked in known open items).

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

## Toolbar layout (MainWindow.axaml)

Toolbar buttons are grouped into **6 rounded boxes** (`Border` with `Background="#14808080"`,
`CornerRadius="4"`, `Padding="1"`) for visual containment. Groups and their buttons:

1. **Refresh** — Refresh
2. **Create** — + Secret, + Cert
3. **Item Actions** — Edit, Enable/Disable, Delete
4. **Share** — Copy, Link, Save
5. **Copy As** — ENV, Docker, K8s, Name
6. **Settings** — Settings, Help

Favorite is standalone (with a thin separator), between Copy-As and Settings.

The toolbar is wrapped in a `ScrollViewer` with `AllowAutoHide="True"` so the horizontal
scrollbar only appears on hover. Global scrollbar styling in `App.axaml` forces horizontal
scrollbars to `Height=6`.

## Vault auth flow

`VaultAccessTokenCredential` (Library) bridges MSAL-based `VaultAccess` to `TokenCredential`.
The `GetTokenAsync` path:

1. **First attempt:** `ChainedTokenCredential(AzureCliCredential, ManagedIdentityCredential)`
   — uses `az login` tokens on dev machines; uses managed identity when running in Azure.
2. **On `OperationCanceledException`:** re-thrown immediately so Cancel button works.
3. **On `CredentialUnavailableException` or other errors:** falls through to the MSAL
   `VaultAccess` chain (certificate / client-credential / user-interactive).

When opening a vault picked from the subscription browser, `MainWindowViewModel.OpenVaultTabAsync`
constructs the `VaultsConfig` in memory — no `Vaults.json` file is read — so the app works
without any local config file.

## Vault Unreachable dialog

When `OpenVaultTabAsync` catches a non-cancellation exception:
1. Removes the speculatively added tab
2. Sets `VaultConnectionError` (binds to an orange ⚠ button next to the vault dropdown)
3. Shows `VaultUnreachableDialogView` via `ShowVaultUnreachableInteraction`

The dialog offers **Retry** (loops with incrementing attempt counter), **Back to vault picker**
(reopens the Subscriptions Manager), and **Cancel** (closes the dialog).

## Help dialog

`ShowHelpInteraction` opens `HelpDialogView` — a dedicated Window (not a `MessageDialog`)
with structured sections (Keyboard shortcuts, Getting started, Searching, Copy formats,
Source code/Issues). Links are clickable via `Process.Start(new ProcessStartInfo { FileName=url, UseShellExecute=true })`
with `xdg-open` fallback for Linux.

## Keyboard shortcuts (MainWindow.axaml.cs → OnKeyDown)

Both `KeyModifiers.Control` (Win/Linux) and `KeyModifiers.Meta` (macOS ⌘) are accepted for
the "command" modifier. Currently wired:

- `F5` → Refresh
- `Delete` → Delete
- `Enter` → Edit (only when focus is not inside a `TextBox`)
- `Ctrl/⌘+F` → Focus search box
- `Ctrl/⌘+C` → Copy value (only when focus is not inside a `TextBox`)

## Themes (Vault/Explorer/Themes/)

Four `ResourceDictionary` files override `SystemAccentColor` + `SystemRegionBrush` +
`SystemControlBackgroundBaseLowBrush` for strong visual distinction (not just accent tint).

| Theme | Variant | Accent | Base | Vibe |
|-------|---------|--------|------|------|
| Arctic Frost (default) | Light | Steel blue `#4a6fa5` | white | Crisp cool |
| Modern Minimalist | Light | Slate gray `#708090` | white | Neutral |
| Ocean Depths | Dark | Cyan-teal `#00b8d4` | deep navy `#0e1d28` | Maritime |
| Midnight Galaxy | Dark | Violet `#9c6dff` | warm purple-black `#1a1425` | Cosmic |

WCAG-AA contrast is targeted on all accent/base pairs.

## Linux packaging

`Vault/Explorer/packaging/linux/vault-explorer.desktop` — install to
`~/.local/share/applications/` so GNOME/KDE shells show the correct taskbar icon
(`Window.Icon` alone isn't enough). See `packaging/linux/README.md` for install steps.

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

## MCP Tooling (Copilot CLI & Claude Code)

Two MCP servers are configured in `.copilot/mcp-config.json` for use with GitHub Copilot CLI,
and in `.claude.json` (project scope) for Claude Code.

| Server | Type | Endpoint | Purpose |
|--------|------|----------|---------|
| `avalonia-docs` | HTTP | `https://docs-mcp.avaloniaui.net/mcp` | Avalonia UI docs — AXAML syntax, controls, binding, theming |
| `azure-mcp` | local (stdio) | `npx -y @azure/mcp@latest server start` | Azure resource management — list vaults, subscriptions, access policies |

**`avalonia-docs`** — invoke when writing or debugging AXAML, asking about control properties,
ReactiveUI/DynamicData patterns, or any Avalonia-specific API.

**`azure-mcp`** — invoke to query live Azure resources (requires `az login`). Useful for
verifying vault access, listing secrets, or inspecting subscription structure.

Copilot CLI config: `~/.copilot/mcp-config.json` (user-level) + `.copilot/mcp-config.json` (repo-level)

---

## Build State & Warnings

```bash
dotnet build Vault/Explorer/VaultExplorer.csproj
# Result: 0 errors, ~169 warnings
```

Warning categories (all non-blocking):

| Category | Count | Root cause | Files |
|----------|-------|-----------|-------|
| `CS8632` — nullable reference | ~42 | `?` annotation outside `#nullable enable` | `MainWindowViewModel.cs`, dialog VMs |
| `CS0618` — obsolete Avalonia APIs | ~25 | `DragEventArgs.Data`, `DataFormats.Files/Text`, `IClipboard.SetDataObjectAsync` | `MainWindow.axaml.cs`, `AvaloniaClipboardService.cs` |
| `CS0618` — DynamicData `.Sort()` | 1 | Should use `.SortAndBind()` in DynamicData 9.4+ | `VaultListViewModel.cs:116` |
| `CA1416` — platform-specific | 1 | Linux-only service; correct at runtime | `ProtocolHandlerServiceFactory.cs` |

---

## Testing

**No automated tests exist.** The project has no test projects; all testing is manual.

If adding tests, recommended stack:
- **Unit**: xUnit 2.x + `ReactiveUI.Testing` for ViewModel assertions
- **Mock Azure SDK**: `Azure.Core.TestFramework` or plain `Moq`
- **Project name**: `Vault/VaultExplorer.Tests/`

---

## Coding conventions

- Namespace prefix: `Microsoft.Vault.*`
- `async void` only in Avalonia event handlers; wrap body in try-catch.
- All user-visible strings are inline (no resource file abstraction).
- No `*.Designer.cs` or `*.resx` files — all removed in the WinForms→Avalonia migration.
- AXAML `Kind` attributes for `MaterialIcon` inside `DataTemplate` must use
  `{x:Static materialIcons:MaterialIconKind.IconName}` — plain string values don't work in
  compiled DataTemplates.

## Known open items

- `vault://` protocol handler: Windows registry registration works; macOS `.plist` registration
  works; **Linux `.desktop` MIME registration not yet implemented** —
  `Services/LinuxProtocolHandlerService.cs` throws `NotSupportedException`.
- Notification service (`Services/AvaloniaNotificationService.cs:14`) is a Phase 3 stub — calls
  log to `Debug.WriteLine` only. Planned: integrate `Notification.Avalonia` NuGet for native OS
  toasts (Windows: Toast, Linux: D-Bus, macOS: NSUserNotification).
- Certificate picker service (`Services/AvaloniaCertificatePickerService.cs:19`) is a Phase 3 stub
  — throws `NotSupportedException`. Planned: Avalonia Window listing non-expired X509 certs from
  the OS certificate store.
- Secret compression (gzip + base64 content type) has backend methods in `Library/Vault.cs` but is
  **not wired to the UI** — `SecretDialogViewModel` / `SecretDialogView.axaml` have no compression
  toggle.
- Obsolete Avalonia drag-drop APIs in `MainWindow.axaml.cs` (`DragEventArgs.Data`,
  `DataFormats.Files`) — emit CS0618 warnings; functional but should migrate to Avalonia 11.4+ API.
- DynamicData `Sort()` + `Bind()` in `VaultListViewModel.cs:116` should migrate to `SortAndBind()`
  (DynamicData 9.4+ preferred API).
- `ClearClipboard.exe` is Windows-only — clipboard auto-clear silently skips on Linux/macOS.
- PowerShell integration was removed (the upstream WinForms implementation doesn't port cleanly).
- No automated tests — the entire app is manually tested.
