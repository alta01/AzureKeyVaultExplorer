// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Vault.Explorer.Views.Dialogs
{
    using Avalonia.ReactiveUI;
    using Microsoft.Vault.Explorer.ViewModels;
    using ReactiveUI;
    using System.Reactive.Disposables;

    public partial class SubscriptionsManagerView : ReactiveWindow<SubscriptionsManagerViewModel>
    {
        public SubscriptionsManagerView()
        {
            InitializeComponent();

            this.WhenActivated(d =>
            {
                // Close the window with the selected VaultAlias when Confirm fires
                ViewModel!.ConfirmCommand
                    .Subscribe(alias => Close(alias))
                    .DisposeWith(d);
            });
        }

        private void OkButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
            => Close(ViewModel?.CurrentVaultAlias);

        private void CancelButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
            => Close(null);

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            ViewModel?.Dispose();
        }
    }
}
