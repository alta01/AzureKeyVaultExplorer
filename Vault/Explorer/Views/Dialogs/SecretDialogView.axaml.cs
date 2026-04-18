// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Vault.Explorer.Views.Dialogs
{
    using System.Reactive.Disposables;
    using System.Reactive.Linq;
    using Avalonia.Controls;
    using Avalonia.ReactiveUI;
    using AvaloniaEdit;
    using Microsoft.Vault.Explorer.ViewModels;
    using ReactiveUI;

    public partial class SecretDialogView : ReactiveWindow<SecretDialogViewModel>
    {
        public SecretDialogView()
        {
            InitializeComponent();

            this.WhenActivated(d =>
            {
                // Two-way sync between AvaloniaEdit and the ViewModel's ValueText
                ValueEditor.TextChanged += OnEditorTextChanged;

                // When VM changes ValueText (e.g. mask toggle, version load) push it into the editor
                this.WhenAnyValue(v => v.ViewModel!.ValueText)
                    .Subscribe(text =>
                    {
                        if (ValueEditor.Text != text)
                        {
                            ValueEditor.Document.Text = text;
                            // Pitfall: clear undo stack so masked value is not recoverable
                            ValueEditor.Document.UndoStack.ClearAll();
                        }
                    })
                    .DisposeWith(d);

                Disposable.Create(() => ValueEditor.TextChanged -= OnEditorTextChanged)
                    .DisposeWith(d);
            });
        }

        private void OnEditorTextChanged(object? sender, EventArgs e)
        {
            if (ViewModel != null)
                ViewModel.ValueText = ValueEditor.Text;
        }

        private async void OkButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (ViewModel?.CanSave != true) return;
            try
            {
                var result = await ViewModel.SaveCommand.Execute().FirstAsync();
                if (result != null)
                    Close(true);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Save failed: {ex}");
            }
        }

        private void CancelButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
            => Close(false);
    }
}
