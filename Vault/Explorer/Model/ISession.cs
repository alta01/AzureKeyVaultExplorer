// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Vault.Explorer.Model
{
    using Microsoft.Vault.Explorer.Controls.Lists;
    using Microsoft.Vault.Explorer.Model.Files.Aliases;
    using Microsoft.Vault.Explorer.ViewModels;

    public interface ISession
    {
        VaultAlias CurrentVaultAlias { get; }

        Library.Vault CurrentVault { get; }

        /// <summary>WinForms list control — removed in Phase 5.</summary>
        ListViewSecrets ListViewSecrets { get; }

        /// <summary>Avalonia MVVM list ViewModel — wired in Phase 4; available from Phase 2.</summary>
        VaultListViewModel VaultListViewModel { get; }
    }
}