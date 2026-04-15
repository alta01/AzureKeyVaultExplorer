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
        }

        private void OnDataGridUnloaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (sender is not DataGrid grid) return;
            grid.RemoveHandler(DragDrop.DropEvent, OnGridDrop);
            grid.RemoveHandler(DragDrop.DragOverEvent, OnGridDragOver);
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
                case Key.F when (e.KeyModifiers & KeyModifiers.Control) != 0:
                    // Ctrl+F: focus search box
                    this.FindControl<TextBox>("SearchBox")?.Focus();
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
