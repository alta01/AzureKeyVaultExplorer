// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Vault.Explorer.ViewModels
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Reactive;
    using System.Reactive.Disposables;
    using System.Reactive.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Avalonia.Controls.ApplicationLifetimes;
    using Avalonia.Platform.Storage;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Vault.Explorer.Common;
    using Microsoft.Vault.Explorer.Model;
    using Microsoft.Vault.Explorer.Model.ContentTypes;
    using Microsoft.Vault.Explorer.Model.Files.Aliases;
    using Microsoft.Vault.Explorer.Services;
    using Microsoft.Vault.Library;
    using ReactiveUI;

    /// <summary>
    /// ViewModel for the main application window.
    /// Replaces WinForms <c>MainForm</c> as the central MVVM controller.
    /// Implements <see cref="ISession"/> so it can be passed to <see cref="VaultItemViewModel"/>s.
    /// </summary>
    public sealed class MainWindowViewModel : ViewModelBase, ISession, IDisposable
    {
        private readonly IDialogService _dialogs;
        private readonly IClipboardService _clipboard;
        private readonly INotificationService _notifications;
        private readonly CompositeDisposable _disposables = new();

        // ── ISession ───────────────────────────────────────────────────────────
        VaultAlias ISession.CurrentVaultAlias => CurrentVaultAlias!;
        Library.Vault ISession.CurrentVault => CurrentVault!;
        public VaultListViewModel VaultListViewModel { get; } = new();

        // ── Public vault state (used by AXAML bindings and ISession) ───────────
        public VaultAlias? CurrentVaultAlias { get; private set; }
        public Library.Vault? CurrentVault { get; private set; }

        // ── Window title ───────────────────────────────────────────────────────
        private string _title = Globals.AppName;
        public string Title
        {
            get => _title;
            private set => this.RaiseAndSetIfChanged(ref _title, value);
        }

        // ── Vault alias selector ───────────────────────────────────────────────
        public ObservableCollection<object> VaultAliases { get; } = new();
        private const string AddNewVaultText = "How to add new vault here...";
        private const string PickVaultText = "Pick vault from subscription...";
        private readonly Dictionary<string, VaultAlias> _tempVaultAliases = new();

        private object? _selectedVaultAlias;
        public object? SelectedVaultAlias
        {
            get => _selectedVaultAlias;
            set
            {
                this.RaiseAndSetIfChanged(ref _selectedVaultAlias, value);
                _ = OnVaultAliasSelectedAsync(value);
            }
        }

        // ── Item selection ─────────────────────────────────────────────────────
        private VaultItemViewModel? _selectedItem;
        public VaultItemViewModel? SelectedItem
        {
            get => _selectedItem;
            set => this.RaiseAndSetIfChanged(ref _selectedItem, value);
        }

        private IList<VaultItemViewModel> _selectedItems = Array.Empty<VaultItemViewModel>();
        public IList<VaultItemViewModel> SelectedItems
        {
            get => _selectedItems;
            set { _selectedItems = value; this.RaisePropertyChanged(); UpdateSelectedCountText(); }
        }

        // ── Busy / Status ──────────────────────────────────────────────────────
        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            private set => this.RaiseAndSetIfChanged(ref _isBusy, value);
        }

        private string _statusText = "";
        public string StatusText
        {
            get => _statusText;
            private set => this.RaiseAndSetIfChanged(ref _statusText, value);
        }

        private string _itemCountText = "";
        public string ItemCountText
        {
            get => _itemCountText;
            private set => this.RaiseAndSetIfChanged(ref _itemCountText, value);
        }

        private string _selectedCountText = "0 selected";
        public string SelectedCountText
        {
            get => _selectedCountText;
            private set => this.RaiseAndSetIfChanged(ref _selectedCountText, value);
        }

        private CancellationTokenSource? _cts;

        // ── Per-selection state ────────────────────────────────────────────────
        private bool _isFavorite;
        public bool IsFavorite
        {
            get => _isFavorite;
            private set => this.RaiseAndSetIfChanged(ref _isFavorite, value);
        }

        private string _toggleText = "Disable";
        public string ToggleText
        {
            get => _toggleText;
            private set => this.RaiseAndSetIfChanged(ref _toggleText, value);
        }

        // ── Commands ───────────────────────────────────────────────────────────
        public ReactiveCommand<Unit, Unit> RefreshCommand { get; }
        public ReactiveCommand<Unit, Unit> AddSecretCommand { get; }
        public ReactiveCommand<Unit, Unit> AddCertFromFileCommand { get; }
        public ReactiveCommand<Unit, Unit> EditCommand { get; }
        public ReactiveCommand<Unit, Unit> ToggleCommand { get; }
        public ReactiveCommand<Unit, Unit> DeleteCommand { get; }
        public ReactiveCommand<Unit, Unit> CopyValueCommand { get; }
        public ReactiveCommand<Unit, Unit> CopyLinkCommand { get; }
        public ReactiveCommand<Unit, Unit> SaveToFileCommand { get; }
        public ReactiveCommand<Unit, Unit> ExportTsvCommand { get; }
        public ReactiveCommand<Unit, Unit> CopyAsEnvVarCommand { get; }
        public ReactiveCommand<Unit, Unit> CopyAsDockerEnvCommand { get; }
        public ReactiveCommand<Unit, Unit> CopyAsK8sYamlCommand { get; }
        public ReactiveCommand<Unit, Unit> CopyNameCommand { get; }
        public ReactiveCommand<Unit, Unit> ToggleFavoriteCommand { get; }
        public ReactiveCommand<Unit, Unit> SettingsCommand { get; }
        public ReactiveCommand<Unit, Unit> HelpCommand { get; }
        public ReactiveCommand<Unit, Unit> CancelCommand { get; }
        public ReactiveCommand<Unit, Unit> ExitCommand { get; }

        // ── Interactions (wired in MainWindow.axaml.cs) ───────────────────────
        public Interaction<Unit, VaultAlias?> PickVaultInteraction { get; } = new();
        public Interaction<FilePickerOpenOptions, string?> OpenFileInteraction { get; } = new();
        public Interaction<FilePickerSaveOptions, string?> SaveFileInteraction { get; } = new();
        /// <summary>Input = null for new secret, VaultItemViewModel for edit.</summary>
        public Interaction<VaultItemViewModel?, bool> ShowSecretDialogInteraction { get; } = new();
        /// <summary>Input = null for new certificate, VaultItemViewModel for edit.</summary>
        public Interaction<VaultItemViewModel?, bool> ShowCertDialogInteraction { get; } = new();

        // ── Constructor ────────────────────────────────────────────────────────

        public MainWindowViewModel(
            IDialogService dialogs,
            IClipboardService clipboard,
            INotificationService notifications)
        {
            _dialogs = dialogs;
            _clipboard = clipboard;
            _notifications = notifications;

            // canExecute predicates
            var hasVault = this.WhenAnyValue(x => x.CurrentVault).Select(v => v != null);
            var notBusy = this.WhenAnyValue(x => x.IsBusy).Select(b => !b);
            var canOperate = Observable.CombineLatest(hasVault, notBusy, (v, nb) => v && nb);
            var hasSingleItem = this.WhenAnyValue(x => x.SelectedItem).Select(v => v != null);
            var hasSingleActive = this.WhenAnyValue(x => x.SelectedItem)
                .Select(v => v != null && v.Enabled && v.Active);
            var hasAnySelection = this.WhenAnyValue(x => x.SelectedItems)
                .Select(items => items != null && items.Count > 0);

            RefreshCommand = ReactiveCommand.CreateFromTask(RefreshAsync, canOperate);
            AddSecretCommand = ReactiveCommand.CreateFromTask(AddSecretAsync, canOperate);
            AddCertFromFileCommand = ReactiveCommand.CreateFromTask(AddCertFromFileAsync, canOperate);
            EditCommand = ReactiveCommand.CreateFromTask(EditItemAsync, hasSingleItem);
            ToggleCommand = ReactiveCommand.CreateFromTask(ToggleItemAsync, hasSingleItem);
            DeleteCommand = ReactiveCommand.CreateFromTask(DeleteItemsAsync, hasAnySelection);
            CopyValueCommand = ReactiveCommand.CreateFromTask(CopyValueAsync, hasSingleActive);
            CopyLinkCommand = ReactiveCommand.CreateFromTask(CopyLinkAsync, hasSingleItem);
            SaveToFileCommand = ReactiveCommand.CreateFromTask(SaveToFileAsync, hasSingleActive);
            ExportTsvCommand = ReactiveCommand.CreateFromTask(ExportTsvAsync, hasVault);
            CopyAsEnvVarCommand = ReactiveCommand.CreateFromTask(CopyAsEnvVarAsync, hasSingleActive);
            CopyAsDockerEnvCommand = ReactiveCommand.CreateFromTask(CopyAsDockerEnvAsync, hasSingleActive);
            CopyAsK8sYamlCommand = ReactiveCommand.CreateFromTask(CopyAsK8sYamlAsync, hasSingleActive);
            CopyNameCommand = ReactiveCommand.CreateFromTask(CopyNameAsync, hasSingleItem);
            ToggleFavoriteCommand = ReactiveCommand.Create(ToggleFavorite, hasAnySelection);
            SettingsCommand = ReactiveCommand.CreateFromTask(OpenSettingsAsync);
            HelpCommand = ReactiveCommand.CreateFromTask(ShowHelpAsync);
            CancelCommand = ReactiveCommand.Create(Cancel, this.WhenAnyValue(x => x.IsBusy));
            ExitCommand = ReactiveCommand.Create(() =>
            {
                if (Avalonia.Application.Current?.ApplicationLifetime
                    is IClassicDesktopStyleApplicationLifetime lt)
                    lt.Shutdown();
                else
                    Environment.Exit(0);
            });

            // Wire error toasts for all commands
            new ReactiveCommand<Unit, Unit>[]
            {
                RefreshCommand, AddSecretCommand, AddCertFromFileCommand,
                EditCommand, ToggleCommand, DeleteCommand,
                CopyValueCommand, CopyLinkCommand, SaveToFileCommand, ExportTsvCommand,
                CopyAsEnvVarCommand, CopyAsDockerEnvCommand, CopyAsK8sYamlCommand,
                CopyNameCommand, SettingsCommand
            }
            .ToObservable()
            .SelectMany(cmd => cmd.ThrownExceptions)
            .Subscribe(ex => _ = _dialogs.ShowErrorAsync("Operation failed", ex.Message, ex))
            .DisposeWith(_disposables);

            // Keep selection-state properties in sync
            this.WhenAnyValue(x => x.SelectedItem)
                .Subscribe(item =>
                {
                    IsFavorite = item?.IsFavorite ?? false;
                    ToggleText = (item?.Enabled ?? true) ? "Disable" : "Enable";
                })
                .DisposeWith(_disposables);

            // Update item count whenever the collection changes
            ((System.Collections.Specialized.INotifyCollectionChanged)VaultListViewModel.Items)
                .CollectionChanged += (_, _) => UpdateItemCountText();

            PopulateVaultAliases();
        }

        // ── Vault alias management ─────────────────────────────────────────────

        private void PopulateVaultAliases()
        {
            VaultAliases.Clear();
            try
            {
                var aliases = Common.Utils.LoadFromJsonFile<VaultAliases>(
                    AppSettings.Default.VaultAliasesJsonFileLocation, isOptional: true);
                foreach (var alias in aliases)
                {
                    alias.IsNew = false;
                    VaultAliases.Add(alias);
                }
            }
            catch { /* Config file not found — start with empty list */ }

            foreach (var alias in _tempVaultAliases.Values)
                VaultAliases.Add(alias);

            VaultAliases.Add(AddNewVaultText);
            VaultAliases.Add(PickVaultText);

            // Restore last used vault
            string lastAlias = AppSettings.Default.LastUsedVaultAlias ?? "";
            if (!string.IsNullOrEmpty(lastAlias))
            {
                var match = VaultAliases.OfType<VaultAlias>()
                    .FirstOrDefault(v => string.Equals(v.Alias, lastAlias, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                {
                    CurrentVaultAlias = match;
                    _selectedVaultAlias = match;
                    this.RaisePropertyChanged(nameof(SelectedVaultAlias));
                    _ = RefreshAsync();
                }
            }
        }

        private async Task OnVaultAliasSelectedAsync(object? item)
        {
            if (item == null) return;

            if (item is string s)
            {
                if (s == AddNewVaultText)
                {
                    await ShowAddVaultHelpAsync();
                    _selectedVaultAlias = CurrentVaultAlias;
                    this.RaisePropertyChanged(nameof(SelectedVaultAlias));
                    return;
                }

                if (s == PickVaultText)
                {
                    await PickVaultFromSubscriptionAsync();
                    return;
                }
                return;
            }

            if (item is not VaultAlias alias) return;

            if (string.Equals(CurrentVaultAlias?.Alias, alias.Alias, StringComparison.OrdinalIgnoreCase)
                && VaultListViewModel.Items.Count > 0)
                return;

            CurrentVaultAlias = alias;
            AppSettings.Default.LastUsedVaultAlias = alias.Alias;
            AppSettings.Default.Save();
            await RefreshAsync();
        }

        private async Task PickVaultFromSubscriptionAsync()
        {
            try
            {
                var alias = await PickVaultInteraction.Handle(Unit.Default);
                if (alias == null)
                {
                    _selectedVaultAlias = CurrentVaultAlias;
                    this.RaisePropertyChanged(nameof(SelectedVaultAlias));
                    return;
                }

                _tempVaultAliases[alias.Alias] = alias;
                int insertAt = Math.Max(0, VaultAliases.Count - 2);
                VaultAliases.Insert(insertAt, alias);

                CurrentVaultAlias = alias;
                _selectedVaultAlias = alias;
                this.RaisePropertyChanged(nameof(SelectedVaultAlias));
                await RefreshAsync();
            }
            catch
            {
                _selectedVaultAlias = CurrentVaultAlias;
                this.RaisePropertyChanged(nameof(SelectedVaultAlias));
            }
        }

        // ── Refresh ────────────────────────────────────────────────────────────

        private async Task RefreshAsync(CancellationToken _ = default)
        {
            if (CurrentVaultAlias == null) return;

            using var cts = new CancellationTokenSource();
            _cts = cts;
            IsBusy = true;
            StatusText = "Connecting to vault...";
            VaultListViewModel.Clear();
            SelectedItem = null;
            Title = Globals.AppName;

            try
            {
                var vault = new Library.Vault(
                    Common.Utils.FullPathToJsonFile(AppSettings.Default.VaultsJsonFileLocation),
                    VaultAccessTypeEnum.ReadWrite,
                    CurrentVaultAlias.VaultNames);

                if (!string.IsNullOrWhiteSpace(CurrentVaultAlias.UserAlias)
                    || vault.VaultsConfig.Count == 0)
                {
                    vault.VaultsConfig[CurrentVaultAlias.VaultNames[0]] = new VaultAccessType(
                        new VaultAccess[] { new VaultAccessUserInteractive(CurrentVaultAlias.DomainHint, CurrentVaultAlias.UserAlias) },
                        new VaultAccess[] { new VaultAccessUserInteractive(CurrentVaultAlias.DomainHint, CurrentVaultAlias.UserAlias) });
                }

                CurrentVault = vault;
                CurrentVaultAlias.SecretsCollectionEnabled = false;
                CurrentVaultAlias.CertificatesCollectionEnabled = false;

                int s = 0, c = 0;

                var secretsTask = Task.Run(async () =>
                {
                    var secrets = await CurrentVault.ListSecretsAsync(0, p =>
                    {
                        s = p;
                        Avalonia.Threading.Dispatcher.UIThread.Post(
                            () => StatusText = $"Loading... {s + c} items");
                    }, cancellationToken: cts.Token);

                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        foreach (var sp in secrets)
                            VaultListViewModel.AddOrReplace(VaultSecretViewModel.FromSecretProperties(this, sp));
                        CurrentVaultAlias.SecretsCollectionEnabled = true;
                    });
                }, cts.Token);

                var certsTask = Task.Run(async () =>
                {
                    var certs = await CurrentVault.ListCertificatesAsync(0, p =>
                    {
                        c = p;
                        Avalonia.Threading.Dispatcher.UIThread.Post(
                            () => StatusText = $"Loading... {s + c} items");
                    }, cancellationToken: cts.Token);

                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        foreach (var cp in certs)
                            VaultListViewModel.AddOrReplace(VaultCertificateViewModel.FromCertificateProperties(this, cp));
                        CurrentVaultAlias.CertificatesCollectionEnabled = true;
                    });
                }, cts.Token);

                await Task.WhenAll(secretsTask, certsTask);

                if (!string.IsNullOrWhiteSpace(CurrentVault.AuthenticatedUserName))
                    Title = $"{Globals.AppName} ({CurrentVault.AuthenticatedUserName})";

                UpdateItemCountText();
            }
            catch (OperationCanceledException) { }
            finally
            {
                IsBusy = false;
                StatusText = "";
                _cts = null;
            }
        }

        // ── Add operations ─────────────────────────────────────────────────────

        private async Task AddSecretAsync(CancellationToken _ = default)
        {
            bool saved = await ShowSecretDialogInteraction.Handle(null);
            if (saved) await RefreshAsync();
        }

        private async Task AddCertFromFileAsync(CancellationToken _ = default)
        {
            var opts = new FilePickerOpenOptions
            {
                Title = "Open Certificate or Secret File",
                AllowMultiple = false,
            };
            string? path = await OpenFileInteraction.Handle(opts);
            if (path == null) return;

            bool saved = await ShowCertDialogInteraction.Handle(null);
            if (saved) await RefreshAsync();
        }

        // ── Edit ───────────────────────────────────────────────────────────────

        private async Task EditItemAsync(CancellationToken _ = default)
        {
            var item = SelectedItem;
            if (item == null) return;

            if (!item.Active && item.Enabled)
            {
                bool proceed = await _dialogs.ShowConfirmAsync(
                    Globals.AppName,
                    $"'{item.Name}' is not active or expired. Do you want to extend its expiration by one year to edit it?");
                if (!proceed) return;
            }

            bool saved = item is VaultCertificateViewModel
                ? await ShowCertDialogInteraction.Handle(item)
                : await ShowSecretDialogInteraction.Handle(item);

            if (saved)
                await RefreshAsync();
        }

        // ── Toggle (Enable / Disable) ──────────────────────────────────────────

        private async Task ToggleItemAsync(CancellationToken _ = default)
        {
            var item = SelectedItem;
            if (item == null) return;

            string action = item.Enabled ? "disable" : "enable";
            bool confirmed = await _dialogs.ShowConfirmAsync(
                Globals.AppName,
                $"Are you sure you want to {action} '{item.Name}'?");
            if (!confirmed) return;

            IsBusy = true;
            StatusText = $"{(item.Enabled ? "Disabling" : "Enabling")} '{item.Name}'...";
            try
            {
                var updated = await item.ToggleAsync(CancellationToken.None);
                VaultListViewModel.AddOrReplace(updated);
                if (SelectedItem?.Name == updated.Name) SelectedItem = updated;
            }
            finally
            {
                IsBusy = false;
                StatusText = "";
            }
        }

        // ── Delete ─────────────────────────────────────────────────────────────

        private async Task DeleteItemsAsync(CancellationToken _ = default)
        {
            var items = SelectedItems.ToList();
            if (items.Count == 0) return;

            string names = string.Join(", ", items.Select(i => i.Name));
            bool confirmed = await _dialogs.ShowConfirmAsync(
                Globals.AppName,
                $"Are you sure you want to delete {items.Count} item(s)?\n{names}\n\nWarning: This cannot be undone!");
            if (!confirmed) return;

            IsBusy = true;
            try
            {
                foreach (var item in items)
                {
                    StatusText = $"Deleting '{item.Name}'...";
                    await item.DeleteAsync(CancellationToken.None);
                    VaultListViewModel.Remove(item.Name);
                }

                SelectedItem = null;
                UpdateItemCountText();
            }
            finally
            {
                IsBusy = false;
                StatusText = "";
            }
        }

        // ── Copy operations ────────────────────────────────────────────────────

        private async Task CopyValueAsync(CancellationToken _ = default)
        {
            var item = SelectedItem;
            if (item == null || CurrentVault == null) return;
            IsBusy = true;
            StatusText = $"Getting '{item.Name}'...";
            try
            {
                var po = await item.GetAsync(CancellationToken.None);
                po.CopyToClipboard(_clipboard, _notifications, false);
            }
            finally { IsBusy = false; StatusText = ""; }
        }

        private async Task CopyLinkAsync(CancellationToken _ = default)
        {
            var item = SelectedItem;
            if (item == null) return;
            await _clipboard.SetHyperlinkAsync(item.Link, item.Name);
        }

        private async Task CopyNameAsync(CancellationToken _ = default)
        {
            var item = SelectedItem;
            if (item == null) return;
            await _clipboard.SetTextAsync(item.Name);
        }

        private async Task CopyAsEnvVarAsync(CancellationToken _ = default)
        {
            var (envName, value) = await GetItemEnvNameAndValueAsync();
            if (value == null) return;
            await _clipboard.SetTextAsync($"{envName}={value}");
        }

        private async Task CopyAsDockerEnvAsync(CancellationToken _ = default)
        {
            var (envName, value) = await GetItemEnvNameAndValueAsync();
            if (value == null) return;
            await _clipboard.SetTextAsync($"--env {envName}={value}");
        }

        private async Task CopyAsK8sYamlAsync(CancellationToken _ = default)
        {
            var item = SelectedItem;
            if (item == null || CurrentVault == null) return;
            IsBusy = true;
            StatusText = $"Getting '{item.Name}'...";
            try
            {
                var po = await item.GetAsync(CancellationToken.None);
                string envName = item.Name.Replace('-', '_').ToLowerInvariant();
                string yaml = $"apiVersion: v1\nkind: Secret\nmetadata:\n  name: {envName}\nstringData:\n  {item.Name}: {po.Value}";
                await _clipboard.SetTextAsync(yaml);
            }
            finally { IsBusy = false; StatusText = ""; }
        }

        private async Task<(string envName, string? value)> GetItemEnvNameAndValueAsync()
        {
            var item = SelectedItem;
            if (item == null || CurrentVault == null) return ("", null);
            IsBusy = true;
            StatusText = $"Getting '{item.Name}'...";
            try
            {
                var po = await item.GetAsync(CancellationToken.None);
                string envName = item.Name.Replace('-', '_').ToUpperInvariant();
                return (envName, po.Value);
            }
            finally { IsBusy = false; StatusText = ""; }
        }

        // ── Save to file ───────────────────────────────────────────────────────

        private async Task SaveToFileAsync(CancellationToken _ = default)
        {
            var item = SelectedItem;
            if (item == null || CurrentVault == null) return;
            IsBusy = true;
            StatusText = $"Getting '{item.Name}'...";
            try
            {
                var po = await item.GetAsync(CancellationToken.None);
                var opts = new FilePickerSaveOptions
                {
                    Title = "Save Secret to File",
                    SuggestedFileName = po.GetFileName(),
                    DefaultExtension = po.GetContentType().ToExtension(),
                };
                string? path = await SaveFileInteraction.Handle(opts);
                if (path != null)
                    po.SaveToFile(path);
            }
            finally { IsBusy = false; StatusText = ""; }
        }

        // ── Export TSV ─────────────────────────────────────────────────────────

        private async Task ExportTsvAsync(CancellationToken _ = default)
        {
            var opts = new FilePickerSaveOptions
            {
                Title = "Export to TSV",
                SuggestedFileName = $"{CurrentVaultAlias?.Alias}_{DateTime.Now:yyyy-MM-dd}",
                DefaultExtension = ".tsv",
            };
            string? path = await SaveFileInteraction.Handle(opts);
            if (path != null)
                File.WriteAllText(path, VaultListViewModel.ExportToTsv());
        }

        // ── Favorites ──────────────────────────────────────────────────────────

        private void ToggleFavorite()
        {
            var items = SelectedItems.ToList();
            VaultListViewModel.ToggleFavorites(items);
            IsFavorite = SelectedItem?.IsFavorite ?? false;
            AppSettings.Default.Save();
        }

        // ── Settings / Help ────────────────────────────────────────────────────

        private async Task OpenSettingsAsync(CancellationToken _ = default)
        {
            var vm = new SettingsViewModel();
            var dlg = new Views.Dialogs.SettingsView { DataContext = vm };
            var owner = (Avalonia.Application.Current?.ApplicationLifetime
                as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
            if (owner != null)
                await dlg.ShowDialog(owner);
        }

        private async Task ShowAddVaultHelpAsync()
        {
            var dialogs = App.Services.GetRequiredService<Services.IDialogService>();
            await dialogs.ShowMessageAsync(
                "How to Add a Vault",
                "There are two ways to add a vault to the dropdown:\n\n" +
                "1. Pick from Subscription (recommended)\n" +
                "   Select 'Pick vault from subscription...' in this dropdown,\n" +
                "   sign in with your Azure account, and choose a subscription\n" +
                "   and vault. It will be saved automatically.\n\n" +
                "2. Edit VaultAliases.json manually\n" +
                "   Open Settings → About → Settings Folder to find your\n" +
                "   config folder. Edit VaultAliases.json to add vault entries.\n" +
                "   Each entry needs a Name, TenantId, SubscriptionId, and\n" +
                "   ResourceGroupName.");
        }

        private async Task ShowHelpAsync()
        {
            var dialogs = App.Services.GetRequiredService<Services.IDialogService>();
            await dialogs.ShowMessageAsync(
                "Azure Key Vault Explorer — Help",
                "KEYBOARD SHORTCUTS\n" +
                "  F5       Refresh the current vault\n" +
                "  Enter    Edit selected item\n" +
                "  Delete   Delete selected item(s)\n" +
                "  Ctrl+F   Focus the search box\n\n" +
                "GETTING STARTED\n" +
                "  1. Select a vault from the dropdown (top-left)\n" +
                "  2. Choose 'Pick vault from subscription...' to browse your\n" +
                "     Azure subscriptions and add a vault\n" +
                "  3. Once a vault is loaded, use the toolbar or Edit menu\n" +
                "     to manage secrets and certificates\n\n" +
                "SEARCHING\n" +
                "  The search box supports regular expressions.\n" +
                "  Example: ^prod- matches all items starting with 'prod-'\n\n" +
                "COPY FORMATS\n" +
                "  Copy as ENV  →  SECRETNAME=value\n" +
                "  Copy as Docker  →  --env SECRETNAME=value\n" +
                "  Copy as K8s  →  Kubernetes secret YAML block\n\n" +
                $"SOURCE CODE & ISSUES\n" +
                $"  {Globals.GitHubUrl}\n\n" +
                "VERSION\n" +
                $"  .NET {Environment.Version}  |  {(Environment.Is64BitProcess ? "x64" : "x86")}  |  {Environment.OSVersion}");
        }

        // ── Cancel ─────────────────────────────────────────────────────────────

        private void Cancel() => _cts?.Cancel();

        // ── Helpers ────────────────────────────────────────────────────────────

        private void UpdateItemCountText()
        {
            int total = VaultListViewModel.Items.Count;
            string vaultPrefix = CurrentVaultAlias != null ? $"{CurrentVaultAlias.Alias} — " : "";
            string searchText = VaultListViewModel.SearchText;
            if (string.IsNullOrWhiteSpace(searchText))
                ItemCountText = $"{vaultPrefix}{total} items";
            else
            {
                int searchCount = VaultListViewModel.SearchResultItems.Count;
                ItemCountText = $"{vaultPrefix}{searchCount} of {total} items";
            }
        }

        private void UpdateSelectedCountText()
        {
            int count = SelectedItems?.Count ?? 0;
            SelectedCountText = count == 0 ? "" : $"{count} selected";
        }

        public void Dispose()
        {
            _disposables.Dispose();
            VaultListViewModel.Dispose();
        }
    }
}
