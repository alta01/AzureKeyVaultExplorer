---
description: "Avalonia UI migration guide for Azure Key Vault Explorer. Use this when working on any part of the WinForms → Avalonia migration on branch claude/avalonia-migration-effort-pILGp."
allowed-tools: Read, Edit, Write, Glob, Grep, Bash, Agent, TodoWrite
---

# Avalonia Migration — Azure Key Vault Explorer

## Context

Migrating from **Windows Forms** to **Avalonia UI 11** with **ReactiveUI** for cross-platform support (Windows + macOS + Linux).

- **Branch**: `claude/avalonia-migration-effort-pILGp`
- **Target framework**: `net10.0` (cross-platform, Phase 5)
- **MVVM**: ReactiveUI + DynamicData (`SourceList<T>` for reactive filtering)
- **Current state**: Phase 0 complete — packages added, scaffold created, WinForms still runs the app

## Folder Conventions

```
Vault/Explorer/
├── App.axaml                    # Avalonia app definition (Phase 4 activates this)
├── App.axaml.cs                 # DI container setup, service registration
├── Views/                       # .axaml files + code-behind only (no business logic)
│   ├── MainWindow.axaml         # Phase 4
│   └── Dialogs/                 # Phase 3
│       ├── SecretDialogView.axaml
│       ├── CertificateDialogView.axaml
│       ├── SubscriptionsManagerView.axaml
│       ├── SettingsView.axaml
│       ├── ExceptionDialogView.axaml
│       └── PasswordDialogView.axaml
├── ViewModels/                  # One ViewModel per View; no System.Windows.Forms imports allowed
│   ├── ViewModelBase.cs         # extends ReactiveObject ✓ (Phase 0)
│   ├── MainWindowViewModel.cs   # Phase 4
│   ├── VaultListViewModel.cs    # Phase 2
│   ├── VaultItemViewModel.cs    # Phase 2
│   ├── VaultSecretViewModel.cs  # Phase 2
│   └── VaultCertificateViewModel.cs  # Phase 2
└── Services/                    # Interfaces + cross-platform implementations
    ├── INotificationService.cs  # Phase 1
    ├── ICertificatePickerService.cs   # Phase 1
    ├── IClipboardService.cs     # Phase 1
    ├── IDialogService.cs        # Phase 1
    ├── IIdleDetectionService.cs # Phase 1
    └── IProtocolHandlerService.cs     # Phase 1
```

## Control Mapping: WinForms → Avalonia

| WinForms | Avalonia |
|---|---|
| `Form` | `Window` |
| `UserControl` | `UserControl` |
| `ListView` + `ListViewItem` | `DataGrid` or `ListBox` + `DataTemplate` |
| `PropertyGrid` | `Avalonia.PropertyGrid` NuGet |
| `Scintilla` (ScintillaNET) | `AvaloniaEdit.TextEditor` |
| `MenuStrip` / `ToolStrip` | `Menu` / `ToolBar` |
| `StatusStrip` | `StatusBar` |
| `SplitContainer` | `Grid` + `GridSplitter` |
| `Panel` | `Panel` or `DockPanel` |
| `GroupBox` | `GroupBox` |
| `ImageList` | Assets via `avares://` URI |
| `NullableDateTimePicker` | `DatePicker` + `CheckBox` (inline AXAML) |
| `AutoClosingMessageBox` | `IDialogService.ShowAutoClosingConfirmAsync()` |
| `MessageBox.Show()` | `IDialogService.ShowMessageAsync()` |
| `OpenFileDialog` | `StorageProvider.OpenFilePickerAsync()` |
| `FolderBrowserDialog` | `StorageProvider.OpenFolderPickerAsync()` |
| `SaveFileDialog` | `StorageProvider.SaveFilePickerAsync()` |

## Service Injection Pattern

Services are registered in `App.axaml.cs → ConfigureServices()` and accessed via:

```csharp
// In ViewModels — receive via constructor injection:
public class MyViewModel : ViewModelBase
{
    public MyViewModel(IDialogService dialogs, IClipboardService clipboard) { ... }
}

// Register in App.axaml.cs:
services.AddSingleton<IDialogService, AvaloniaDialogService>();
services.AddTransient<MyViewModel>();

// In code-behind (last resort only):
var svc = App.Services.GetRequiredService<IDialogService>();
```

## Windows-Specific Replacements

| Windows API | Cross-Platform Replacement |
|---|---|
| `Windows.UI.Notifications.ToastNotificationManager` | `Notification.Avalonia` NuGet |
| `X509Certificate2UI.SelectFromCollection()` | Custom Avalonia `Window` dialog backed by `X509Store` |
| `DataObject` + `DragDropEffects` | Avalonia `TopLevel.Clipboard` API |
| `IMessageFilter` (WM_ messages) | `InputManager.Current.Process` observable |
| `Registry.CurrentUser` (vault:// protocol) | `IProtocolHandlerService` — platform-conditional |
| `ProtectedData.Protect` (DPAPI) | `Microsoft.AspNetCore.DataProtection` |
| `ApplicationSettingsBase` / `user.config` | JSON `AppSettings` in `SpecialFolder.ApplicationData` |
| `Microsoft.Identity.Client.Desktop` (WAM) | `Microsoft.Identity.Client` system browser only |

## ReactiveUI Patterns

### Observable Properties
```csharp
// Option A — manual backing field (no source generator needed):
private string _searchText = "";
public string SearchText
{
    get => _searchText;
    set => this.RaiseAndSetIfChanged(ref _searchText, value);
}

// Option B — [Reactive] attribute (requires ReactiveUI.Fody package):
[Reactive] public string SearchText { get; set; } = "";
```

### Commands
```csharp
// Async command with cancellation:
public ReactiveCommand<Unit, Unit> RefreshCommand { get; }

RefreshCommand = ReactiveCommand.CreateFromTask(
    async ct => await LoadVaultItemsAsync(ct),
    canExecute: this.WhenAnyValue(x => x.CurrentVault, v => v != null));

// Track execution state (for busy indicator):
RefreshCommand.IsExecuting.ToPropertyEx(this, x => x.IsRefreshing);
```

### Reactive Filtering with DynamicData
```csharp
private readonly SourceList<VaultItemViewModel> _source = new();

// Filtered + sorted read-only list for the UI:
public ReadOnlyObservableCollection<VaultItemViewModel> Items { get; }

// In constructor:
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

### WhenActivated (for dialog/window lifecycle)
```csharp
// In View code-behind:
this.WhenActivated(disposables =>
{
    this.OneWayBind(ViewModel, vm => vm.Items, v => v.ItemsList.ItemsSource)
        .DisposeWith(disposables);
    this.BindCommand(ViewModel, vm => vm.RefreshCommand, v => v.RefreshButton)
        .DisposeWith(disposables);
});
```

### Thread Safety Rule
**Always** push UI mutations onto the main thread:
```csharp
// In ReactiveUI pipelines: .ObserveOn(RxApp.MainThreadScheduler)
// Elsewhere: Dispatcher.UIThread.InvokeAsync(() => { ... })
```

## Testing ViewModels

ViewModels must be unit-testable without any UI. Setup:
```csharp
// In test project setup (xUnit IAsyncLifetime or constructor):
RxApp.MainThreadScheduler = Scheduler.CurrentThread;
RxApp.TaskpoolScheduler = Scheduler.CurrentThread;
```

Then instantiate the ViewModel directly with mock services:
```csharp
var vm = new VaultListViewModel(new FakeVault(), new FakeDialogService());
await vm.RefreshCommand.Execute();
Assert.Equal(3, vm.Items.Count);
```

## Known Pitfalls

1. **AvaloniaEdit undo buffer** — when masking/revealing secret values, clear the undo history (`textEditor.Document.UndoStack.ClearAll()`) to prevent the unmasked value being recoverable.

2. **X509Certificate2UI HWND** — `SelectFromCollection` needs a parent window handle. On Windows, pass `window.TryGetPlatformHandle()?.Handle`. On other platforms this is not needed (use the custom dialog).

3. **OLE clipboard drop effect** — The "cut" trick for vault items dragged to Windows Explorer requires `DataObject("Preferred DropEffect", DragDropEffects.Move)` which is Windows-only COM. Use `IClipboardService` and implement a Windows-only conditional.

4. **user.config migration** — On first run after upgrade, check if `%LOCALAPPDATA%\VaultExplorerNext\settings.json` exists. If not, attempt to read from `ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.PerUserRoamingAndLocal)` and migrate values.

5. **App.axaml must be set as AvaloniaXaml** — In the `.csproj`, ensure:
   ```xml
   <AvaloniaXaml Update="App.axaml">
     <Generator>MSBuild:Compile</Generator>
   </AvaloniaXaml>
   ```

6. **`avares://` image URIs** — Reference embedded assets as:
   ```xml
   <Image Source="avares://VaultExplorer/Assets/secret-enabled.png" />
   ```
   Add to `.csproj`: `<AvaloniaResource Include="Assets\**" />`

7. **ReactiveUI and `IScheduler`** — Never use `Task.Run` directly in a ViewModel for work that updates UI. Use `ReactiveCommand.CreateFromObservable` or `CreateFromTask` which automatically marshal back to the UI thread.

---

## Per-Phase Checklist

### Phase 0 — Scaffold ✅
- [x] Branch `claude/avalonia-migration-effort-pILGp` exists
- [x] Avalonia 11.x packages added to `VaultExplorer.csproj`
- [x] `App.axaml` + `App.axaml.cs` created (not wired as entry point)
- [x] `ViewModels/ViewModelBase.cs` created
- [x] `Views/` and `Services/` directories created
- [x] This skill file created at `.claude/commands/avalonia-migration.md`
- [ ] Spike `AvaloniaEdit` syntax highlighting config (verify API before Phase 3)
- [ ] Spike `Avalonia.PropertyGrid` custom editor behavior (verify before Phase 3)

### Phase 1 — Service Abstractions ✅
- [x] `Services/INotificationService.cs` + `AvaloniaNotificationService.cs`
- [x] `Services/ICertificatePickerService.cs` + `AvaloniaCertificatePickerService.cs`
- [x] `Services/IClipboardService.cs` + `AvaloniaClipboardService.cs`
- [x] `Services/IDialogService.cs` + `AvaloniaDialogService.cs`
- [x] `Services/IIdleDetectionService.cs` + `AvaloniaIdleDetectionService.cs`
- [x] `Services/IProtocolHandlerService.cs` + platform impls
- [x] `MemoryTokenCache.cs` DPAPI → `Microsoft.AspNetCore.DataProtection`
- [x] `VaultAccessUserInteractive` — remove `Microsoft.Identity.Client.Desktop`
- [x] `Settings.cs` → `AppSettings` JSON-backed + `SettingsMigrator.cs`
- [x] `PropertyObject*.cs` — strip `System.Windows.Forms` imports
- [x] `VaultLibrary.csproj` — remove `<UseWindowsForms>`

### Phase 2 — ViewModel Layer ✅
- [x] `ViewModels/VaultItemViewModel.cs`
- [x] `ViewModels/VaultSecretViewModel.cs`
- [x] `ViewModels/VaultCertificateViewModel.cs`
- [x] `ViewModels/VaultListViewModel.cs` with DynamicData filtering
- [x] `ISession.cs` updated

### Phase 3 — Dialogs ✅
- [x] ExceptionDialogView.axaml
- [x] PasswordDialogView.axaml
- [x] SettingsView.axaml + SettingsViewModel.cs
- [x] AutoClosingMessageBox → `IDialogService`
- [x] SecretDialogView.axaml + SecretDialogViewModel.cs
- [x] CertificateDialogView.axaml + CertificateDialogViewModel.cs
- [x] SubscriptionsManagerView.axaml + SubscriptionsManagerViewModel.cs

### Phase 4 — Main Window Cut-Over ✅
- [x] `Views/MainWindow.axaml` (full layout: MenuBar, toolbar, DataGrid, PropertyGrid, StatusBar)
- [x] `ViewModels/MainWindowViewModel.cs` (ReactiveCommands, Interactions, ISession impl)
- [x] `Program.cs` rewritten as Avalonia entry point (`BuildAvaloniaApp` + `StartWithClassicDesktopLifetime`)
- [x] `App.axaml.cs` DI wired up (all 6 services + MainWindowViewModel)
- [x] Drag-and-drop implemented (OnGridDrop routes to SecretDialog/CertDialog interaction)
- [x] Keyboard shortcuts: F5=Refresh, Delete=Delete, Ctrl+F=SearchBox focus
- [x] `ExpiresDisplay` property added to `VaultItemViewModel` for DataGrid binding
- [ ] Icon assets migrated to `avares://` (deferred — placeholder bindings in place)
- [ ] Full app smoke test (requires dotnet on target machine)

### Phase 5 — WinForms Removal ✅
- [x] `<UseWindowsForms>` removed from `VaultExplorer.csproj`
- [x] All `*.Designer.cs` deleted (11 files)
- [x] All `*.resx` deleted (10 files)
- [x] `using System.Windows.Forms` swept from all remaining files
- [x] `using System.Drawing.Design` swept (UITypeEditor attrs removed from all collections)
- [x] `ExpandableCollectionEditor` deleted (relied on Windows-only `CollectionEditor`)
- [x] Target framework changed to `net10.0`
- [x] `Microsoft.Identity.Client.Desktop` removed
- [x] `fernandreu.ScintillaNET` removed
- [x] `ISession.ListViewSecrets` removed
- [x] `SettingsMigrator` simplified (legacy `Settings` class source removed)
- [ ] `dotnet list package` verification (deferred — requires dotnet on target machine)
