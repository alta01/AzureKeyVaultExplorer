// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Vault.Explorer.Views.Dialogs
{
    using System;
    using System.Diagnostics;
    using Avalonia.Controls;
    using Avalonia.Interactivity;

    public partial class HelpDialogView : Window
    {
        public HelpDialogView()
        {
            InitializeComponent();
            var versionText = this.FindControl<TextBlock>("VersionInfoText");
            if (versionText != null)
            {
                versionText.Text =
                    $".NET {Environment.Version}  |  " +
                    $"{(Environment.Is64BitProcess ? "x64" : "x86")}  |  " +
                    $"{Environment.OSVersion}";
            }
        }

        private void CloseButton_Click(object? sender, RoutedEventArgs e) => Close();

        private void GitHubLink_Click(object? sender, RoutedEventArgs e)
            => OpenUrl("https://github.com/alta01/AzureKeyVaultExplorer");

        private void IssuesLink_Click(object? sender, RoutedEventArgs e)
            => OpenUrl("https://github.com/alta01/AzureKeyVaultExplorer/issues");

        private static void OpenUrl(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true,
                });
            }
            catch
            {
                // Best-effort — some Linux shells may fail; fall back to xdg-open
                try { Process.Start("xdg-open", url); } catch { /* ignored */ }
            }
        }
    }
}
