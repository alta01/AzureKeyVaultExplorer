// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Vault.Explorer.Views.Dialogs
{
    using Avalonia.Controls;
    using Avalonia.Threading;

    public enum MessageDialogKind { Info, Warning, Error, Confirm }

    public partial class MessageDialogView : Window
    {
        private DispatcherTimer? _autoCloseTimer;

        public MessageDialogView() => InitializeComponent();

        public static MessageDialogView Create(
            string title,
            string message,
            MessageDialogKind kind = MessageDialogKind.Info,
            TimeSpan autoCloseAfter = default)
        {
            var dlg = new MessageDialogView();
            dlg.Title = title;
            dlg.MessageText.Text = message;
            dlg.IconText.Text = kind switch
            {
                MessageDialogKind.Info => "ℹ",
                MessageDialogKind.Warning => "⚠",
                MessageDialogKind.Error => "✖",
                MessageDialogKind.Confirm => "❓",
                _ => "ℹ",
            };

            if (kind == MessageDialogKind.Confirm)
            {
                dlg.YesButton.IsVisible = true;
                dlg.NoButton.IsVisible = true;
                dlg.OkButton.IsVisible = false;
            }

            if (autoCloseAfter > TimeSpan.Zero)
            {
                dlg._autoCloseTimer = new DispatcherTimer
                {
                    Interval = autoCloseAfter
                };
                dlg._autoCloseTimer.Tick += (_, _) =>
                {
                    dlg._autoCloseTimer.Stop();
                    dlg.Close(false);
                };
            }

            return dlg;
        }

        protected override void OnOpened(EventArgs e)
        {
            base.OnOpened(e);
            _autoCloseTimer?.Start();
        }

        private void OkButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
            => Close(true);

        private void YesButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
            => Close(true);

        private void NoButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
            => Close(false);
    }
}
