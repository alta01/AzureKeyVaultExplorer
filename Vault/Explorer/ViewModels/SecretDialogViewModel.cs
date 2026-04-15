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
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Security.KeyVault.Secrets;
    using Microsoft.Vault.Explorer.Common;
    using Microsoft.Vault.Explorer.Controls.MenuItems;
    using Microsoft.Vault.Explorer.Model.Collections;
    using Microsoft.Vault.Explorer.Dialogs;
    using Microsoft.Vault.Explorer.Model;
    using Microsoft.Vault.Explorer.Model.ContentTypes;
    using Microsoft.Vault.Explorer.Model.Files.Secrets;
    using Microsoft.Vault.Explorer.Model.PropObjects;
    using Microsoft.Vault.Explorer.Services;
    using Microsoft.Vault.Library;
    using ReactiveUI;

    /// <summary>
    /// ViewModel for the secret editor dialog.
    /// Replaces the WinForms <see cref="Dialogs.Secrets.SecretDialog"/> for the Avalonia UI layer.
    /// </summary>
    public sealed class SecretDialogViewModel : ViewModelBase
    {
        private readonly ISession _session;
        private readonly IDialogService _dialogs;
        private readonly ItemDialogBaseMode _mode;

        // ── Backing for original object (null on New) ──────────────────────────
        public KeyVaultSecret? OriginalSecret { get; private set; }

        // ── PropertyObject (drives PropertyGrid) ──────────────────────────────
        private PropertyObjectSecret _propertyObject;
        public PropertyObjectSecret PropertyObject
        {
            get => _propertyObject;
            private set
            {
                this.RaiseAndSetIfChanged(ref _propertyObject, value);
                // Re-subscribe to change notifications on the new object
                _propertyObject.PropertyChanged += OnPropertyObjectChanged;
                InvalidateCanSave();
            }
        }

        // ── Name ───────────────────────────────────────────────────────────────
        private string _name = "";
        public string Name
        {
            get => _name;
            set
            {
                this.RaiseAndSetIfChanged(ref _name, value);
                PropertyObject.Name = value;
                _changed = true;
                NameError = PropertyObject.IsNameValid
                    ? null
                    : $"Name must match the following regex:\n{PropertyObject.SecretKind.NameRegex}";
                InvalidateCanSave();
            }
        }

        private string? _nameError;
        public string? NameError
        {
            get => _nameError;
            private set => this.RaiseAndSetIfChanged(ref _nameError, value);
        }

        // ── Value text (what the editor displays) ─────────────────────────────
        private string _valueText = "";
        public string ValueText
        {
            get => _valueText;
            set
            {
                if (_isMasked) return; // Don't propagate masked bullets back
                this.RaiseAndSetIfChanged(ref _valueText, value);
                PropertyObject.Value = value;
                _changed = true;
                UpdateByteCounter();
                InvalidateCanSave();
            }
        }

        // ── Masking ────────────────────────────────────────────────────────────
        private bool _isMasked;
        private string _maskedRealValue = "";

        public bool IsMasked
        {
            get => _isMasked;
            private set => this.RaiseAndSetIfChanged(ref _isMasked, value);
        }

        // ── Certificate mode ───────────────────────────────────────────────────
        private bool _isCertificateMode;
        public bool IsCertificateMode
        {
            get => _isCertificateMode;
            private set => this.RaiseAndSetIfChanged(ref _isCertificateMode, value);
        }

        private CertificateValueObject? _certificateObj;
        public CertificateValueObject? CertificateObject
        {
            get => _certificateObj;
            private set => this.RaiseAndSetIfChanged(ref _certificateObj, value);
        }

        // ── Byte counter ───────────────────────────────────────────────────────
        private string _byteCounterText = "";
        public string ByteCounterText
        {
            get => _byteCounterText;
            private set => this.RaiseAndSetIfChanged(ref _byteCounterText, value);
        }

        private string? _valueError;
        public string? ValueError
        {
            get => _valueError;
            private set => this.RaiseAndSetIfChanged(ref _valueError, value);
        }

        // ── Secret kinds ───────────────────────────────────────────────────────
        public ObservableCollection<SecretKind> SecretKinds { get; } = new();

        private SecretKind? _selectedSecretKind;
        public SecretKind? SelectedSecretKind
        {
            get => _selectedSecretKind;
            set
            {
                if (value == null || _selectedSecretKind == value) return;
                this.RaiseAndSetIfChanged(ref _selectedSecretKind, value);
                ApplySecretKind(value);
            }
        }

        // ── Versions (Edit mode) ───────────────────────────────────────────────
        public ObservableCollection<SecretVersionItem> Versions { get; } = new();

        private SecretVersionItem? _selectedVersion;
        public SecretVersionItem? SelectedVersion
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
        public ReactiveCommand<Unit, VaultSecretViewModel?> SaveCommand { get; }
        public ReactiveCommand<Unit, Unit> ToggleMaskCommand { get; }
        public ReactiveCommand<Unit, Unit> NewPasswordCommand { get; }
        public ReactiveCommand<Unit, Unit> NewGuidCommand { get; }
        public ReactiveCommand<Unit, Unit> NewApiKeyCommand { get; }

        // ── Constructors ───────────────────────────────────────────────────────

        private SecretDialogViewModel(ISession session, IDialogService dialogs, ItemDialogBaseMode mode)
        {
            _session = session;
            _dialogs = dialogs;
            _mode = mode;

            var canSaveObs = this.WhenAnyValue(x => x.CanSave);
            SaveCommand = ReactiveCommand.CreateFromTask<Unit, VaultSecretViewModel?>(
                _ => ExecuteSaveAsync(), canSaveObs);

            ToggleMaskCommand = ReactiveCommand.Create(ToggleMask);
            NewPasswordCommand = ReactiveCommand.Create(
                () => { if (!IsCertificateMode) SetValue(Common.Utils.NewSecurePassword()); });
            NewGuidCommand = ReactiveCommand.Create(
                () => { if (!IsCertificateMode) SetValue(Guid.NewGuid().ToString("D")); });
            NewApiKeyCommand = ReactiveCommand.Create(
                () => { if (!IsCertificateMode) SetValue(Explorer.Common.Utils.NewApiKey()); });

            // Load secret kinds
            LoadSecretKinds();
        }

        /// <summary>New empty secret.</summary>
        public SecretDialogViewModel(ISession session, IDialogService dialogs)
            : this(session, dialogs, ItemDialogBaseMode.New)
        {
            _changed = true;
            _propertyObject = new PropertyObjectSecret(
                ContentTypeEnumConverter.GetDescription(ContentType.Text),
                OnPropertyObjectChanged);
            _propertyObject.PropertyChanged += OnPropertyObjectChanged;
            _name = _propertyObject.Name;
            _valueText = _propertyObject.Value ?? "";
            ApplyDefaultSecretKind();
            IsCertificateMode = ((PropertyObjectSecret)_propertyObject).ContentType.IsCertificate();
            UpdateByteCounter();
        }

        /// <summary>New secret from file.</summary>
        public SecretDialogViewModel(ISession session, IDialogService dialogs, FileInfo fi)
            : this(session, dialogs)
        {
            var obj = (PropertyObjectSecret)_propertyObject;
            Name = Explorer.Common.Utils.ConvertToValidSecretName(Path.GetFileNameWithoutExtension(fi.Name));
            obj.ContentType = ContentTypeUtils.FromExtension(fi.Extension);

            switch (obj.ContentType)
            {
                case ContentType.KeyVaultSecret:
                    var kvsf = Explorer.Common.Utils.LoadFromJsonFile<KeyVaultSecretFile>(fi.FullName);
                    _propertyObject = new PropertyObjectSecret(kvsf.Deserialize(), OnPropertyObjectChanged);
                    _propertyObject.PropertyChanged += OnPropertyObjectChanged;
                    _name = _propertyObject.Name;
                    _valueText = _propertyObject.Value ?? "";
                    break;
                case ContentType.Certificate:
                case ContentType.Pkcs12:
                case ContentType.Pkcs12Base64:
                    // Password prompt happens in the View; the View calls SetCertificateObject()
                    // after obtaining the password from PasswordDialogView.
                    break;
                default:
                    SetValue(File.ReadAllText(fi.FullName));
                    break;
            }
        }

        /// <summary>Edit existing secret — loads the first (latest) version.</summary>
        public SecretDialogViewModel(
            ISession session, IDialogService dialogs,
            string name, IEnumerable<SecretProperties> versions)
            : this(session, dialogs, ItemDialogBaseMode.Edit)
        {
            _changed = false;

            // Populate versions list; the View selects [0] which triggers LoadVersionAsync
            int i = 0;
            foreach (var v in versions.OrderByDescending(v => v.CreatedOn))
                Versions.Add(new SecretVersionItem(i++, v));

            // Bootstrap with an empty PropertyObject; the first version selection will replace it
            _propertyObject = new PropertyObjectSecret(
                ContentTypeEnumConverter.GetDescription(ContentType.Text),
                OnPropertyObjectChanged);
            _propertyObject.PropertyChanged += OnPropertyObjectChanged;
        }

        // ── Public helpers (called by the View) ────────────────────────────────

        public void SetCertificateObject(CertificateValueObject cvo)
        {
            CertificateObject = cvo;
            if (cvo != null)
            {
                cvo.FillTagsAndExpiration(PropertyObject);
                var value = cvo.ToValue(SelectedSecretKind?.CertificateFormat);
                SetValue(value);
            }
            IsCertificateMode = CertificateObject != null;
        }

        // ── Private helpers ────────────────────────────────────────────────────

        private void LoadSecretKinds()
        {
            try
            {
                var allKinds = Explorer.Common.Utils.LoadFromJsonFile<SecretKinds>(
                    AppSettings.Default.SecretKindsJsonFileLocation);

                var alias = _session.CurrentVaultAlias;
                IEnumerable<SecretKind> kinds;
                if (alias.SecretKinds == null ||
                    (alias.SecretKinds.Length == 1 && (string)alias.SecretKinds.GetValue(0) == "Custom"))
                {
                    kinds = allKinds.Values;
                }
                else
                {
                    kinds = alias.SecretKinds
                        .Select(sk => allKinds.TryGetValue(sk, out var k) ? k : null)
                        .Where(k => k != null)!;
                }

                foreach (var k in kinds.OrderBy(k => k.Alias))
                    SecretKinds.Add(k);
            }
            catch { /* loading fails gracefully — Custom kind is always present */ }
        }

        private void ApplyDefaultSecretKind()
        {
            var def = SecretKinds.FirstOrDefault(k => k.Alias == "Custom") ?? SecretKinds.FirstOrDefault();
            if (def != null)
            {
                _selectedSecretKind = def;
                ApplySecretKind(def);
            }
        }

        private void ApplySecretKind(SecretKind sk)
        {
            PropertyObject.AddOrUpdateSecretKind(sk);
            PropertyObject.SecretKind = sk;
            PropertyObject.PopulateCustomTags();
            if (_mode == ItemDialogBaseMode.New)
            {
                PropertyObject.PopulateExpiration();
                if (!string.IsNullOrEmpty(sk.ValueTemplate))
                    SetValue(sk.ValueTemplate);
            }
            this.RaisePropertyChanged(nameof(SelectedSecretKind));
            InvalidateCanSave();
        }

        private void SetValue(string value)
        {
            _isMasked = false;
            IsMasked = false;
            _valueText = value;
            this.RaisePropertyChanged(nameof(ValueText));
            PropertyObject.Value = value;
            _changed = true;
            UpdateByteCounter();
            InvalidateCanSave();
        }

        private void ToggleMask()
        {
            if (_isMasked)
            {
                _isMasked = false;
                IsMasked = false;
                _valueText = _maskedRealValue;
                _maskedRealValue = "";
                this.RaisePropertyChanged(nameof(ValueText));
            }
            else
            {
                _maskedRealValue = _valueText;
                _isMasked = true;
                IsMasked = true;
                _valueText = new string('•', _maskedRealValue.Length);
                this.RaisePropertyChanged(nameof(ValueText));
            }
        }

        private void UpdateByteCounter()
        {
            int len = PropertyObject.RawValue?.Length ?? 0;
            int left = Consts.MaxSecretValueLength - len;
            ByteCounterText = $"{len:N0} bytes / {left:N0} bytes left";
        }

        private void OnPropertyObjectChanged(object? sender, PropertyChangedEventArgs e)
        {
            _changed = true;
            if (e.PropertyName == nameof(PropertyObjectSecret.ContentType))
            {
                IsCertificateMode = ((PropertyObjectSecret)PropertyObject).ContentType.IsCertificate();
                SetCertificateObject(_certificateObj!);
            }
            InvalidateCanSave();
        }

        private void InvalidateCanSave()
        {
            string? tagsError = PropertyObject.AreCustomTagsValid();
            bool valueOk = PropertyObject.IsValueValid &&
                           PropertyObject.RawValue?.Length >= 1 &&
                           PropertyObject.RawValue?.Length <= Consts.MaxSecretValueLength;

            ValueError = valueOk ? null
                : $"Secret value must match: {PropertyObject.SecretKind.ValueRegex}";

            CanSave = _changed &&
                      PropertyObject.IsNameValid &&
                      valueOk &&
                      PropertyObject.IsExpirationValid &&
                      string.IsNullOrEmpty(tagsError);
        }

        private async Task LoadVersionAsync(SecretVersionItem versionItem)
        {
            var s = await _session.CurrentVault.GetSecretAsync(
                versionItem.SecretProperties.Name,
                versionItem.SecretProperties.Version,
                CancellationToken.None);

            OriginalSecret ??= s;

            _propertyObject = new PropertyObjectSecret(s, OnPropertyObjectChanged);
            _propertyObject.PropertyChanged += OnPropertyObjectChanged;
            this.RaisePropertyChanged(nameof(PropertyObject));

            _name = _propertyObject.Name;
            this.RaisePropertyChanged(nameof(Name));

            SetValue(_propertyObject.Value ?? "");
            IsCertificateMode = ((PropertyObjectSecret)_propertyObject).ContentType.IsCertificate();
            AutoDetectSecretKind();
            _changed = false;
            InvalidateCanSave();
        }

        private void AutoDetectSecretKind()
        {
            var obj = (PropertyObjectSecret)PropertyObject;
            var skTag = obj.Tags?.GetOrNull(new TagItem(Consts.SecretKindKey, ""));
            SecretKind? found = skTag != null
                ? SecretKinds.FirstOrDefault(sk => sk.Alias == skTag.Value)
                : null;

            // If cert vs non-cert mismatch with auto-detected kind → fall back to Custom
            if (found != null &&
                obj.ContentType.IsCertificate() != found.IsCertificate)
                found = null;

            _selectedSecretKind = found ?? SecretKinds.FirstOrDefault(k => k.Alias == "Custom") ?? SecretKinds.FirstOrDefault();
            this.RaisePropertyChanged(nameof(SelectedSecretKind));
        }

        private async Task<VaultSecretViewModel?> ExecuteSaveAsync()
        {
            if (!CanSave) return null;
            try
            {
                return await VaultSecretViewModel.SaveAsync(
                    _session, OriginalSecret,
                    (PropertyObjectSecret)PropertyObject,
                    CancellationToken.None);
            }
            catch (Exception ex)
            {
                await _dialogs.ShowErrorAsync("Save failed", ex.Message, ex);
                return null;
            }
        }
    }

    /// <summary>Thin wrapper around <see cref="SecretProperties"/> for display in the versions picker.</summary>
    public sealed class SecretVersionItem
    {
        public readonly int Index;
        public readonly SecretProperties SecretProperties;

        public SecretVersionItem(int index, SecretProperties sp)
        {
            Index = index;
            SecretProperties = sp;
        }

        public override string ToString() =>
            $"v{Index + 1} — {SecretProperties.CreatedOn?.LocalDateTime:g}";
    }
}
