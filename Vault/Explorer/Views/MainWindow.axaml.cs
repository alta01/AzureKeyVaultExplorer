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
    using System.Threading;
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
                        var vm = item == null
                            ? new SecretDialogViewModel(ViewModel, dialogs)
                            : new SecretDialogViewModel(ViewModel, dialogs, item);
                        var dlg = new SecretDialogView { DataContext = vm };
                        bool result = await dlg.ShowDialog<bool>(this);
                        ctx.SetOutput(result);
                    })
                    .DisposeWith(d);

                // ── Certificate dialog ────────────────────────────────────────
                ViewModel.ShowCertDialogInteraction
                    .RegisterHandler(async ctx =>
                    {
                        var item = ctx.Input; // null = new, non-null = edit
                        var dialogs = App.Services.GetRequiredService<IDialogService>();
                        var vm = item == null
                            ? new CertificateDialogViewModel(ViewModel, dialogs)
                            : new CertificateDialogViewModel(ViewModel, dialogs, item);
                        var dlg = new CertificateDialogView { DataContext = vm };
                        bool result = await dlg.ShowDialog<bool>(this);
                        ctx.SetOutput(result);
                    })
                    .DisposeWith(d);

                // ── DataGrid multi-selection → SelectedItems ──────────────────
                var grid = this.FindControl<DataGrid>("ItemsDataGrid");
                if (grid != null)
                {
                    grid.SelectionChanged += OnDataGridSelectionChanged;
                    Disposable.Create(() => grid.SelectionChanged -= OnDataGridSelectionChanged)
                        .DisposeWith(d);
                }

                // ── Drag-and-drop into the list ───────────────────────────────
                if (grid != null)
                {
                    DragDrop.SetAllowDrop(grid, true);
                    grid.AddHandler(DragDrop.DropEvent, OnGridDrop);
                    grid.AddHandler(DragDrop.DragOverEvent, OnGridDragOver);
                    Disposable.Create(() =>
                    {
                        grid.RemoveHandler(DragDrop.DropEvent, OnGridDrop);
                        grid.RemoveHandler(DragDrop.DragOverEvent, OnGridDragOver);
                    }).DisposeWith(d);
                }
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
                case Key.F5 when ViewModel.RefreshCommand.CanExecute(Unit.Default):
                    ViewModel.RefreshCommand.Execute(Unit.Default).Subscribe();
                    e.Handled = true;
                    break;
                case Key.Delete when ViewModel.DeleteCommand.CanExecute(Unit.Default):
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
