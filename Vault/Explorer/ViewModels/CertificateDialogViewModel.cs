// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Vault.Explorer.ViewModels
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.ComponentModel;
    using System.IO;
    using System.Linq;
    using System.Reactive;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Security.KeyVault.Certificates;
    using Microsoft.Vault.Explorer.Common;
    using Microsoft.Vault.Explorer.Controls.MenuItems;
    using Microsoft.Vault.Explorer.Dialogs;
    using Microsoft.Vault.Explorer.Model;
    using Microsoft.Vault.Explorer.Model.ContentTypes;
    using Microsoft.Vault.Explorer.Model.Files.Secrets;
    using Microsoft.Vault.Explorer.Model.PropObjects;
    using Microsoft.Vault.Explorer.Services;
    using ReactiveUI;

    /// <summary>
    /// ViewModel for the certificate editor dialog.
    /// Replaces the WinForms <see cref="Dialogs.Certificates.CertificateDialog"/> for the Avalonia UI.
    /// </summary>
    public sealed class CertificateDialogViewModel : ViewModelBase
    {
        private readonly ISession _session;
        private readonly IDialogService _dialogs;
        private readonly ItemDialogBaseMode _mode;

        public KeyVaultCertificate? OriginalCertificate { get; private set; }

        // ── PropertyObject ─────────────────────────────────────────────────────
        private PropertyObjectCertificate? _propertyObject;
        public PropertyObjectCertificate? PropertyObject
        {
            get => _propertyObject;
            private set
            {
                this.RaiseAndSetIfChanged(ref _propertyObject, value);
                if (_propertyObject != null)
                    _propertyObject.PropertyChanged += OnPropertyObjectChanged;
                InvalidateCanSave();
            }
        }

        // ── Name (read-only in Edit mode) ──────────────────────────────────────
        private string _name = "";
        public string Name
        {
            get => _name;
            set
            {
                this.RaiseAndSetIfChanged(ref _name, value);
                if (_propertyObject != null) _propertyObject.Name = value;
                _changed = true;
                InvalidateCanSave();
            }
        }

        public bool IsNameReadOnly { get; private set; }

        // ── Certificate display info ───────────────────────────────────────────
        private string _certSummary = "";
        public string CertSummary
        {
            get => _certSummary;
            private set => this.RaiseAndSetIfChanged(ref _certSummary, value);
        }

        // ── Versions (Edit mode) ───────────────────────────────────────────────
        public ObservableCollection<CertificateVersionItem> Versions { get; } = new();

        private CertificateVersionItem? _selectedVersion;
        public CertificateVersionItem? SelectedVersion
        {
            get => _selectedVersion;
            set
            {
                if (value == null || _selectedVersion == value) return;
                this.RaiseAndSetIfChanged(ref _selectedVersion, value);
                _ = LoadVersionAsync(value);
            }
        }

        // ── Dirty/valid ────────────────────────────────────────────────────────
        private bool _changed;
        private bool _canSave;
        public bool CanSave
        {
            get => _canSave;
            private set => this.RaiseAndSetIfChanged(ref _canSave, value);
        }

        // ── Commands ───────────────────────────────────────────────────────────
        public ReactiveCommand<Unit, VaultCertificateViewModel?> SaveCommand { get; }

        // ── Constructors ───────────────────────────────────────────────────────

        private CertificateDialogViewModel(ISession session, IDialogService dialogs, ItemDialogBaseMode mode)
        {
            _session = session;
            _dialogs = dialogs;
            _mode = mode;

            var canSaveObs = this.WhenAnyValue(x => x.CanSave);
            SaveCommand = ReactiveCommand.CreateFromTask<Unit, VaultCertificateViewModel?>(
                _ => ExecuteSaveAsync(), canSaveObs);
        }

        /// <summary>New certificate from file.</summary>
        public static async Task<CertificateDialogViewModel> FromFileAsync(
            ISession session, IDialogService dialogs, FileInfo fi)
        {
            var vm = new CertificateDialogViewModel(session, dialogs, ItemDialogBaseMode.New);

            X509Certificate2? cert = null;
            var contentType = ContentTypeUtils.FromExtension(fi.Extension);
            switch (contentType)
            {
                case ContentType.Certificate:
                    cert = X509CertificateLoader.LoadCertificateFromFile(fi.FullName);
                    break;
                case ContentType.Pkcs12:
                    string? password = await dialogs.ShowPasswordDialogAsync();
                    if (password == null) return vm; // cancelled
                    cert = X509CertificateLoader.LoadPkcs12FromFile(
                        fi.FullName, password,
                        X509KeyStorageFlags.UserKeySet | X509KeyStorageFlags.Exportable,
                        Pkcs12LoaderLimits.Defaults);
                    break;
                case ContentType.KeyVaultCertificate:
                    var kvcf = Explorer.Common.Utils.LoadFromJsonFile<KeyVaultCertificateFile>(fi.FullName);
                    cert = X509CertificateLoader.LoadCertificate(kvcf.Deserialize().Cer);
                    break;
                default:
                    throw new ArgumentException($"Unsupported content type {contentType} for certificate import.");
            }

            vm.InitFromCertificate(null, cert);
            return vm;
        }

        /// <summary>New certificate from X509Certificate2.</summary>
        public static CertificateDialogViewModel FromCertificate(
            ISession session, IDialogService dialogs, X509Certificate2 cert)
        {
            var vm = new CertificateDialogViewModel(session, dialogs, ItemDialogBaseMode.New);
            vm.InitFromCertificate(null, cert);
            return vm;
        }

        /// <summary>Edit existing certificate — loads the first (latest) version.</summary>
        public CertificateDialogViewModel(
            ISession session, IDialogService dialogs,
            string name, IEnumerable<CertificateProperties> versions)
            : this(session, dialogs, ItemDialogBaseMode.Edit)
        {
            IsNameReadOnly = true;
            _name = name;
            _changed = false;

            int i = 0;
            foreach (var v in versions.OrderByDescending(v => v.CreatedOn))
                Versions.Add(new CertificateVersionItem(i++, v));
        }

        // ── Private helpers ────────────────────────────────────────────────────

        private CertificatePolicy DefaultPolicy() => new("Self", "CN=Self")
        {
            Exportable = true,
            KeySize = 2048,
            KeyType = "RSA",
            ReuseKey = false,
            ContentType = CertificateContentType.Pkcs12,
        };

        private void InitFromCertificate(KeyVaultCertificate? kvc, X509Certificate2? cert)
        {
            var policy = DefaultPolicy();
            var po = new PropertyObjectCertificate(kvc, policy, cert, OnPropertyObjectChanged);
            PropertyObject = po;
            _name = Explorer.Common.Utils.ConvertToValidSecretName(
                cert?.GetNameInfo(X509NameType.SimpleName, false) ?? "");
            this.RaisePropertyChanged(nameof(Name));
            RefreshCertSummary(cert);
            _changed = kvc == null; // dirty only for new certs
            InvalidateCanSave();
        }

        private void RefreshCertSummary(X509Certificate2? cert)
        {
            if (cert == null) { CertSummary = ""; return; }
            CertSummary =
                $"Subject:    {cert.Subject}\n" +
                $"Issuer:     {cert.Issuer}\n" +
                $"Thumbprint: {cert.Thumbprint?.ToLowerInvariant()}\n" +
                $"Valid from: {cert.NotBefore:g}   to: {cert.NotAfter:g}";
        }

        private async Task LoadVersionAsync(CertificateVersionItem item)
        {
            try
            {
                var kvc = await _session.CurrentVault.GetCertificateAsync(
                    item.Properties.Name, item.Index == 0 ? null : item.Properties.Version,
                    CancellationToken.None);
                var cert = await _session.CurrentVault.GetCertificateWithExportableKeysAsync(
                    item.Properties.Name, item.Properties.Version, CancellationToken.None);

                CertificatePolicy? policy = null;
                if (item.Index == 0) // Only fetch policy for current version
                    policy = await _session.CurrentVault.GetCertificatePolicyAsync(item.Properties.Name, CancellationToken.None);

                OriginalCertificate ??= kvc;

                var po = new PropertyObjectCertificate(kvc, policy ?? DefaultPolicy(), cert, OnPropertyObjectChanged);
                PropertyObject = po;
                _name = po.Name;
                this.RaisePropertyChanged(nameof(Name));
                RefreshCertSummary(cert);
                _changed = false;
                InvalidateCanSave();
            }
            catch (Exception ex)
            {
                await _dialogs.ShowErrorAsync("Load certificate version failed", ex.Message, ex);
            }
        }

        private void OnPropertyObjectChanged(object? sender, PropertyChangedEventArgs e)
        {
            _changed = true;
            InvalidateCanSave();
        }

        private void InvalidateCanSave()
        {
            if (_propertyObject == null) { CanSave = false; return; }
            string? tagsError = _propertyObject.AreCustomTagsValid();
            CanSave = _changed &&
                      _propertyObject.IsNameValid &&
                      _propertyObject.IsExpirationValid &&
                      string.IsNullOrEmpty(tagsError);
        }

        private async Task<VaultCertificateViewModel?> ExecuteSaveAsync()
        {
            if (!CanSave || _propertyObject == null) return null;
            try
            {
                return _mode == ItemDialogBaseMode.New
                    ? await VaultCertificateViewModel.NewAsync(_session, _propertyObject, CancellationToken.None)
                    : await VaultCertificateViewModel.UpdateAsync(_session, _propertyObject, CancellationToken.None);
            }
            catch (Exception ex)
            {
                await _dialogs.ShowErrorAsync("Save failed", ex.Message, ex);
                return null;
            }
        }
    }

    /// <summary>Thin wrapper around <see cref="CertificateProperties"/> for the version picker.</summary>
    public sealed class CertificateVersionItem
    {
        public readonly int Index;
        public readonly CertificateProperties Properties;

        public CertificateVersionItem(int index, CertificateProperties cp)
        {
            Index = index;
            Properties = cp;
        }

        public override string ToString() =>
            $"v{Index + 1} — {Properties.CreatedOn?.LocalDateTime:g}";
    }
}
