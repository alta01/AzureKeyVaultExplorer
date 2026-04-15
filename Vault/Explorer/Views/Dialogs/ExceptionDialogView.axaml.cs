// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Vault.Explorer.Views.Dialogs
{
    using Avalonia.Controls;
    using Microsoft.Vault.Explorer.Common;

    public partial class ExceptionDialogView : Window
    {
        public ExceptionDialogView() => InitializeComponent();

        public ExceptionDialogView(Exception e) : this()
        {
            // Build a readable caption: "Unhandled exception of type <Type>: <Message>"
            CaptionText.Text = $"Oops… Unhandled exception of type {e.GetType().Name} has occurred:\n\n" +
                               $"{e.Message}\n\nTo ignore this error click Continue, otherwise click Quit.";
            DetailsText.Text = e.ToString();
        }

        private void ContinueButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
            => Close();

        private void QuitButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (Avalonia.Application.Current?.ApplicationLifetime is
                Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
                desktop.Shutdown(1);
            else
                Environment.Exit(1);
        }
    }
}
