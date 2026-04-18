// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Vault.Explorer.ViewModels
{
    using System;
    using System.Reactive;
    using ReactiveUI;

    public enum VaultUnreachableAction { Retry, GoBack, Cancel }

    public sealed class VaultUnreachableDialogViewModel : ViewModelBase
    {
        public string VaultName { get; }
        public string ErrorMessage { get; }
        public string FullDetails { get; }

        private int _attemptCount;
        public int AttemptCount
        {
            get => _attemptCount;
            private set
            {
                this.RaiseAndSetIfChanged(ref _attemptCount, value);
                this.RaisePropertyChanged(nameof(RetryLabel));
            }
        }

        private bool _isRetrying;
        public bool IsRetrying
        {
            get => _isRetrying;
            private set => this.RaiseAndSetIfChanged(ref _isRetrying, value);
        }

        public string RetryLabel => AttemptCount == 0 ? "Try again" : $"Try again (attempt {AttemptCount + 1})";

        public ReactiveCommand<Unit, VaultUnreachableAction> RetryCommand { get; }
        public ReactiveCommand<Unit, VaultUnreachableAction> GoBackCommand { get; }
        public ReactiveCommand<Unit, VaultUnreachableAction> CancelCommand { get; }

        public VaultUnreachableDialogViewModel(string vaultName, Exception exception)
        {
            VaultName = vaultName;
            ErrorMessage = exception.Message;
            FullDetails = exception.ToString();

            RetryCommand = ReactiveCommand.Create(() =>
            {
                AttemptCount++;
                IsRetrying = true;
                return VaultUnreachableAction.Retry;
            });

            GoBackCommand = ReactiveCommand.Create(() => VaultUnreachableAction.GoBack);
            CancelCommand = ReactiveCommand.Create(() => VaultUnreachableAction.Cancel);
        }

        /// <summary>Called by MainWindowViewModel after a retry attempt completes (success or fail).</summary>
        public void NotifyRetryComplete() => IsRetrying = false;
    }
}
