// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Vault.Explorer.Views.Dialogs
{
    using Avalonia.ReactiveUI;
    using Microsoft.Vault.Explorer.ViewModels;

    public partial class VaultUnreachableDialogView : ReactiveWindow<VaultUnreachableDialogViewModel>
    {
        public VaultUnreachableDialogView()
        {
            InitializeComponent();
        }

        private void RetryButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
            => Close(VaultUnreachableAction.Retry);

        private void GoBackButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
            => Close(VaultUnreachableAction.GoBack);

        private void CancelButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
            => Close(VaultUnreachableAction.Cancel);
    }
}
