// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Vault.Explorer.Views.Dialogs
{
    using Avalonia.Controls;
    using Avalonia.Input;

    public partial class PasswordDialogView : Window
    {
        public string? Password { get; private set; }

        public PasswordDialogView() => InitializeComponent();

        private void ShowPasswordCheck_Changed(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            bool show = ShowPasswordCheck.IsChecked == true;
            PasswordBox.PasswordChar = show ? '\0' : '•';
        }

        private void PasswordBox_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) Confirm();
        }

        private void OkButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
            => Confirm();

        private void CancelButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
            => Close(null);

        private void Confirm()
        {
            Password = PasswordBox.Text;
            Close(Password);
        }
    }
}
