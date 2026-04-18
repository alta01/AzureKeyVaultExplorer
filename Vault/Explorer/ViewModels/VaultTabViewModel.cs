// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Vault.Explorer.ViewModels
{
    using System;
    using System.Reactive;
    using Microsoft.Vault.Library;
    using Microsoft.Vault.Explorer.Model.Files.Aliases;
    using ReactiveUI;

    /// <summary>
    /// Represents a single open vault tab in the main window.
    /// Each tab owns its own <see cref="VaultListViewModel"/> and item selection state.
    /// </summary>
    public sealed class VaultTabViewModel : ViewModelBase, IDisposable
    {
        // ── Identity ───────────────────────────────────────────────────────────
        public string Header { get; }
        public VaultAlias VaultAlias { get; }
        public Library.Vault Vault { get; }

        // ── Content ────────────────────────────────────────────────────────────
        public VaultListViewModel VaultList { get; } = new();

        // SelectedItem is intentionally not on this VM — the DataGrid in the ContentTemplate
        // binds directly up to MainWindowViewModel.SelectedItem so toolbar canExecute predicates
        // re-enable as soon as a row is selected. Keeping a per-tab selection here would
        // require an extra synchroniser and break command availability.

        // ── Commands ───────────────────────────────────────────────────────────
        public ReactiveCommand<Unit, VaultTabViewModel> CloseCommand { get; }

        // ── Constructor ────────────────────────────────────────────────────────

        public VaultTabViewModel(VaultAlias alias, Library.Vault vault)
        {
            VaultAlias = alias;
            Vault = vault;
            Header = alias.Alias;
            CloseCommand = ReactiveCommand.Create(() => this);
        }

        public void Dispose() => VaultList.Dispose();
    }
}
