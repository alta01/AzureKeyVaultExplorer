// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Vault.Explorer.Views
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reactive;
    using System.Reactive.Disposables;
    using System.Reactive.Linq;
    using System.Threading;
    using Azure.Security.KeyVault.Certificates;
    using Azure.Security.KeyVault.Secrets;
    using Avalonia;
    using Avalonia.Controls;
    using Avalonia.Input;
    using Avalonia.Interactivity;
    using Avalonia.Platform.Storage;
    using Avalonia.ReactiveUI;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Vault.Explorer.Model.ContentTypes;
    using Microsoft.Vault.Explorer.Model.Files.Aliases;
    using Microsoft.Vault.Explorer.Services;
    using Microsoft.Vault.Explorer.ViewModels;
    using Microsoft.Vault.Explorer.Views.Dialogs;
    using ReactiveUI;

    public partial class MainWindow : ReactiveWindow<MainWindowViewModel>
    {
        public MainWindow()
        {
            InitializeComponent();

            this.WhenActivated(d =>
            {
                if (ViewModel == null) return;

                // ── File/folder pickers ───────────────────────────────────────
                ViewModel.OpenFileInteraction
                    .RegisterHandler(async ctx =>
                    {
                        var result = await StorageProvider.OpenFilePickerAsync(ctx.Input);
                        ctx.SetOutput(result.FirstOrDefault()?.TryGetLocalPath());
                    })
                    .DisposeWith(d);

                ViewModel.SaveFileInteraction
                    .RegisterHandler(async ctx =>
                    {
                        var result = await StorageProvider.SaveFilePickerAsync(ctx.Input);
                        ctx.SetOutput(result?.TryGetLocalPath());
                    })
                    .DisposeWith(d);

                // ── Vault picker (SubscriptionsManager dialog) ────────────────
                ViewModel.PickVaultInteraction
                    .RegisterHandler(async ctx =>
                    {
                        var dialogs = App.Services.GetRequiredService<IDialogService>();
                        var vm = new SubscriptionsManagerViewModel(dialogs);
                        var dlg = new SubscriptionsManagerView { DataContext = vm };
                        var alias = await dlg.ShowDialog<VaultAlias?>(this);
                        ctx.SetOutput(alias);
                    })
                    .DisposeWith(d);

                // ── Secret dialog ─────────────────────────────────────────────
                ViewModel.ShowSecretDialogInteraction
                    .RegisterHandler(async ctx =>
                    {
                        var item = ctx.Input; // null = new, non-null = edit
                        var dialogs = App.Services.GetRequiredService<IDialogService>();
                        SecretDialogViewModel vm;
                        if (item == null)
                        {
                            vm = new SecretDialogViewModel(ViewModel, dialogs);
                        }
                        else
                        {
                            var versions = await item.GetVersionsAsync(CancellationToken.None);
                            vm = new SecretDialogViewModel(ViewModel, dialogs, item.Name,
                                versions.OfType<SecretProperties>());
                        }
                        var dlg = new SecretDialogView { DataContext = vm };
                        bool result = await dlg.ShowDialog<bool>(this);
                        ctx.SetOutput(result);
                    })
                    .DisposeWith(d);

                // ── Certificate dialog ────────────────────────────────────────
                ViewModel.ShowCertDialogInteraction
                    .RegisterHandler(async ctx =>
                    {
                        var item = ctx.Input; // null = new (picks file), non-null = edit
                        var dialogs = App.Services.GetRequiredService<IDialogService>();
                        CertificateDialogViewModel vm;
                        if (item == null)
                        {
                            var pickOpts = new FilePickerOpenOptions
                            {
                                Title = "Select certificate file",
                                AllowMultiple = false,
                                FileTypeFilter = new[]
                                {
                                    new FilePickerFileType("Certificate files")
                                    { Patterns = new[] { "*.pfx", "*.p12", "*.cer", "*.crt" } }
                                }
                            };
                            var picked = await StorageProvider.OpenFilePickerAsync(pickOpts);
                            var localPath = picked.FirstOrDefault()?.TryGetLocalPath();
                            if (localPath == null) { ctx.SetOutput(false); return; }
                            vm = await CertificateDialogViewModel.FromFileAsync(
                                ViewModel, dialogs, new FileInfo(localPath));
                        }
                        else
                        {
                            var certVersions = (await item.GetVersionsAsync(CancellationToken.None))
                                .OfType<CertificateProperties>();
                            vm = new CertificateDialogViewModel(ViewModel, dialogs, item.Name, certVersions);
                        }
                        var dlg = new CertificateDialogView { DataContext = vm };
                        bool result = await dlg.ShowDialog<bool>(this);
                        ctx.SetOutput(result);
                    })
                    .DisposeWith(d);

                // ── Vault unreachable dialog ──────────────────────────────────
                ViewModel.ShowVaultUnreachableInteraction
                    .RegisterHandler(async ctx =>
                    {
                        var dlg = new VaultUnreachableDialogView { DataContext = ctx.Input };
                        var result = await dlg.ShowDialog<VaultUnreachableAction>(this);
                        ctx.SetOutput(result);
                    })
                    .DisposeWith(d);

                // ── Help dialog ───────────────────────────────────────────────
                ViewModel.ShowHelpInteraction
                    .RegisterHandler(async ctx =>
                    {
                        var dlg = new HelpDialogView();
                        await dlg.ShowDialog(this);
                        ctx.SetOutput(Unit.Default);
                    })
                    .DisposeWith(d);

                // DataGrid multi-selection and drag-drop are wired via AXAML event handlers
                // (SelectionChanged="OnDataGridSelectionChanged", Drop="OnGridDrop", DragOver="OnGridDragOver")
                // so no FindControl needed here.
            });
        }

        // ── DataGrid selection forwarding ──────────────────────────────────────

        private void OnDataGridSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (ViewModel == null || sender is not DataGrid grid) return;

            var selected = grid.SelectedItems
                .OfType<VaultItemViewModel>()
                .ToList();

            ViewModel.SelectedItems = selected;

            // Drive SelectedItem from the grid's primary selection
            ViewModel.SelectedItem = grid.SelectedItem as VaultItemViewModel;
        }

        // ── DataGrid Loaded/Unloaded — wire drag-drop per tab DataGrid ────────

        private void OnDataGridLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (sender is not DataGrid grid) return;
            DragDrop.SetAllowDrop(grid, true);
            grid.AddHandler(DragDrop.DropEvent, OnGridDrop);
            grid.AddHandler(DragDrop.DragOverEvent, OnGridDragOver);
            grid.ContextRequested += OnDataGridContextRequested;
            grid.Sorting += OnDataGridSorting;
        }

        private void OnDataGridUnloaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (sender is not DataGrid grid) return;
            grid.RemoveHandler(DragDrop.DropEvent, OnGridDrop);
            grid.RemoveHandler(DragDrop.DragOverEvent, OnGridDragOver);
            grid.ContextRequested -= OnDataGridContextRequested;
            grid.Sorting -= OnDataGridSorting;
        }

        // ── DataGrid column-header sort → ViewModel ───────────────────────────

        private void OnDataGridSorting(object? sender, DataGridColumnEventArgs e)
        {
            var list = ViewModel?.VaultListViewModel;
            if (list == null) return;

            var newColumn = e.Column.Header?.ToString() switch
            {
                "Name"       => VaultListSortColumn.Name,
                "Updated"    => VaultListSortColumn.Updated,
                "Changed By" => VaultListSortColumn.ChangedBy,
                "Expires"    => VaultListSortColumn.Expires,
                _            => (VaultListSortColumn?)null,
            };

            if (newColumn == null) return;

            if (list.SortColumn == newColumn.Value)
                list.SortAscending = !list.SortAscending;
            else
            {
                list.SortColumn = newColumn.Value;
                list.SortAscending = true;
            }

            // Cancel DataGrid's built-in sort — DynamicData owns the collection order.
            e.Handled = true;
        }

        // ── Right-click context menu ───────────────────────────────────────────

        private void OnDataGridContextRequested(object? sender, ContextRequestedEventArgs e)
        {
            if (ViewModel == null) return;

            var hasSelection = ViewModel.SelectedItem != null;
            var hasVault = ViewModel.CurrentVault != null;
            var isSecret = ViewModel.SelectedItem is VaultSecretViewModel;
            var isCert   = ViewModel.SelectedItem is VaultCertificateViewModel;

            var menu = new ContextMenu();
            var items = menu.Items;

            // Always-visible vault actions
            items.Add(MenuItem("Refresh vault", ViewModel.RefreshCommand, "Refresh", "F5"));

            if (hasSelection)
            {
                items.Add(new Separator());
                items.Add(MenuItem("Edit...", ViewModel.EditCommand, "PencilOutline", "Enter"));
                items.Add(MenuItem(ViewModel.ToggleText, ViewModel.ToggleCommand, "LockOpenOutline"));
                items.Add(MenuItem("Delete...", ViewModel.DeleteCommand, "TrashCanOutline", "Delete"));
                items.Add(new Separator());
                items.Add(MenuItem("Copy value", ViewModel.CopyValueCommand, "ContentCopy"));
                items.Add(MenuItem("Copy link", ViewModel.CopyLinkCommand, "LinkVariant"));
                items.Add(MenuItem("Save to file...", ViewModel.SaveToFileCommand, "ContentSave"));
                items.Add(new Separator());
                items.Add(MenuItem("Copy as ENV", ViewModel.CopyAsEnvVarCommand, "Console"));
                items.Add(MenuItem("Copy as Docker --env", ViewModel.CopyAsDockerEnvCommand, "Docker"));
                items.Add(MenuItem("Copy as K8s YAML", ViewModel.CopyAsK8sYamlCommand, "Kubernetes"));
                items.Add(MenuItem("Copy name", ViewModel.CopyNameCommand, "KeyVariant"));
            }

            if (hasVault)
            {
                items.Add(new Separator());
                items.Add(MenuItem("Add secret...", ViewModel.AddSecretCommand, "LockPlus"));
                items.Add(MenuItem("Add certificate...", ViewModel.AddCertFromFileCommand, "Certificate"));
            }

            if (hasSelection)
            {
                items.Add(new Separator());
                items.Add(MenuItem("Mark / unmark favourite", ViewModel.ToggleFavoriteCommand, "StarOutline"));
            }

            menu.Open(sender as Control ?? this);
            e.Handled = true;
        }

        private static MenuItem MenuItem(string header,
            System.Windows.Input.ICommand command,
            string iconKind,
            string? inputGesture = null)
        {
            var iconControl = new Material.Icons.Avalonia.MaterialIcon
            {
                Width = 14, Height = 14,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            };
            // Set Kind via reflection to avoid x:Static at runtime
            var kindProp = typeof(Material.Icons.Avalonia.MaterialIcon).GetProperty("Kind");
            if (kindProp != null && Enum.TryParse<Material.Icons.MaterialIconKind>(iconKind, out var kind))
                kindProp.SetValue(iconControl, kind);

            var item = new MenuItem
            {
                Header = header,
                Command = command,
                Icon = iconControl,
            };
            if (inputGesture != null)
                item.InputGesture = KeyGesture.Parse(inputGesture);
            return item;
        }

        // ── Drag-and-drop (drop files onto the list to add them) ──────────────

        private static void OnGridDragOver(object? sender, DragEventArgs e)
        {
            e.DragEffects = e.Data.Contains(DataFormats.Files)
                ? DragDropEffects.Copy
                : DragDropEffects.None;
        }

        private async void OnGridDrop(object? sender, DragEventArgs e)
        {
            if (ViewModel == null) return;
            if (!e.Data.Contains(DataFormats.Files)) return;

            var files = e.Data.GetFiles()?.ToList();
            if (files == null || files.Count == 0) return;

            foreach (var file in files)
            {
                string? path = file.TryGetLocalPath();
                if (path == null) continue;

                var fi = new FileInfo(path);
                switch (ContentTypeUtils.FromExtension(fi.Extension))
                {
                    case ContentType.KeyVaultCertificate:
                    case ContentType.Pkcs12:
                        await ViewModel.ShowCertDialogInteraction.Handle(null);
                        break;
                    default:
                        await ViewModel.ShowSecretDialogInteraction.Handle(null);
                        break;
                }
            }
        }

        // ── Keyboard shortcuts not covered by InputGesture on MenuItems ────────

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (ViewModel == null) return;

            // Accept Ctrl (Windows/Linux) OR Meta (macOS ⌘) as the "command" modifier
            bool cmd = (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Meta)) != 0;

            switch (e.Key)
            {
                case Key.F5:
                    ViewModel.RefreshCommand.Execute(Unit.Default).Subscribe();
                    e.Handled = true;
                    break;
                case Key.Delete:
                    ViewModel.DeleteCommand.Execute(Unit.Default).Subscribe();
                    e.Handled = true;
                    break;
                case Key.Enter when !(FocusManager?.GetFocusedElement() is TextBox):
                    // Enter edits the selected item — but don't swallow Enter while typing
                    ViewModel.EditCommand.Execute(Unit.Default).Subscribe();
                    e.Handled = true;
                    break;
                case Key.F when cmd:
                    // Ctrl+F / ⌘+F: focus search box
                    this.FindControl<TextBox>("SearchBox")?.Focus();
                    e.Handled = true;
                    break;
                case Key.C when cmd && !(FocusManager?.GetFocusedElement() is TextBox):
                    // Ctrl+C / ⌘+C: copy secret value when a list item is selected
                    ViewModel.CopyValueCommand.Execute(Unit.Default).Subscribe();
                    e.Handled = true;
                    break;
            }
        }

        // ── Window closing: save settings ─────────────────────────────────────

        protected override void OnClosing(WindowClosingEventArgs e)
        {
            base.OnClosing(e);
            // Save last-used vault alias
            if (ViewModel?.CurrentVaultAlias != null)
            {
                AppSettings.Default.LastUsedVaultAlias = ViewModel.CurrentVaultAlias.Alias;
                AppSettings.Default.Save();
            }
        }
    }
}
