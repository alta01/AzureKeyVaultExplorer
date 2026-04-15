// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Microsoft.Vault.Explorer.Views.Dialogs;

namespace Microsoft.Vault.Explorer.Services;

/// <summary>
/// Avalonia implementation of <see cref="IDialogService"/>.
/// All dialog windows are shown modal over the main window (or as top-level if no main window).
/// </summary>
public sealed class AvaloniaDialogService : IDialogService
{
    // ── Helper ────────────────────────────────────────────────────────────────

    private static Window? GetMainWindow() =>
        (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)
        ?.MainWindow;

    private static Task<TResult> ShowAsync<TResult>(Window dialog)
    {
        var owner = GetMainWindow();
        return owner != null
            ? dialog.ShowDialog<TResult>(owner)
            : dialog.ShowDialog<TResult>(new Window()); // fallback (should not normally happen)
    }

    // ── IDialogService ────────────────────────────────────────────────────────

    public Task ShowMessageAsync(string title, string message)
    {
        var dlg = MessageDialogView.Create(title, message, MessageDialogKind.Info);
        return ShowAsync<object?>(dlg);
    }

    public Task ShowErrorAsync(string title, string message, Exception? ex = null)
    {
        if (ex != null)
        {
            var dlg = new ExceptionDialogView(ex);
            dlg.Title = title;
            return ShowAsync<object?>(dlg);
        }
        else
        {
            var dlg = MessageDialogView.Create(title, message, MessageDialogKind.Error);
            return ShowAsync<object?>(dlg);
        }
    }

    public async Task<bool> ShowConfirmAsync(string title, string message)
    {
        var dlg = MessageDialogView.Create(title, message, MessageDialogKind.Confirm);
        var result = await ShowAsync<bool>(dlg);
        return result;
    }

    public async Task<bool> ShowAutoClosingConfirmAsync(string title, string message, TimeSpan timeout)
    {
        var dlg = MessageDialogView.Create(title, message, MessageDialogKind.Confirm, timeout);
        var result = await ShowAsync<bool>(dlg);
        return result;
    }

    // ── Password helper (used by SecretDialogViewModel and CertificateDialogViewModel) ─

    public async Task<string?> ShowPasswordDialogAsync()
    {
        var dlg = new PasswordDialogView();
        return await ShowAsync<string?>(dlg);
    }
}
