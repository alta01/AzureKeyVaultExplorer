// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Vault.Explorer.Views.Dialogs
{
    using Avalonia.ReactiveUI;
    using Microsoft.Vault.Explorer.ViewModels;

    public partial class CertificateDialogView : ReactiveWindow<CertificateDialogViewModel>
    {
        public CertificateDialogView() => InitializeComponent();

        private void OkButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
            => Close(true);

        private void CancelButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
            => Close(false);
    }
}
