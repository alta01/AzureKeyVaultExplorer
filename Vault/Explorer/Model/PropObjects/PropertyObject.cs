// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Vault.Explorer.Model.PropObjects
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Drawing.Design;
    using System.IO;
    using Microsoft.Vault.Explorer.Controls;
    using Microsoft.Vault.Explorer.Controls.MenuItems;
    using Microsoft.Vault.Explorer.Model.Collections;
    using Microsoft.Vault.Explorer.Model.ContentTypes;
    using Microsoft.Vault.Explorer.Services;
    using Microsoft.Vault.Library;
    using Utils = Microsoft.Vault.Explorer.Common.Utils;

    /// <summary>
    ///     Base class to edit an object via PropertyGrid / Avalonia.PropertyGrid
    /// </summary>
    [DefaultProperty("Tags")]
    public abstract class PropertyObject : INotifyPropertyChanged
    {
        protected void NotifyPropertyChanged(string info) => this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(info));

        protected ContentType _contentType;
        protected string _version;

        public ContentType GetContentType() => this._contentType;

        public event PropertyChangedEventHandler PropertyChanged;

        public readonly ObjectIdentifier Identifier;

        [DisplayName("Name")]
        [Browsable(false)]
        public string Name { get; set; }

        [Category("General")]
        [DisplayName("Custom Tags")]
        public ObservableTagItemsCollection Tags { get; set; }

        private bool? _enabled;

        [Category("General")]
        [DisplayName("Enabled")]
        public bool? Enabled
        {
            get { return this._enabled; }
            set
            {
                this._enabled = value;
                this.NotifyPropertyChanged(nameof(this.Enabled));
            }
        }

        private DateTime? _notBefore;

        [Category("General")]
        [DisplayName("Valid from time (UTC)")]
        [Editor(typeof(NullableDateTimePickerEditor), typeof(UITypeEditor))]
        public DateTime? NotBefore
        {
            get { return this._notBefore; }
            set
            {
                this._notBefore = value;
                this.NotifyPropertyChanged(nameof(this.NotBefore));
            }
        }

        private DateTime? _expires;

        [Category("General")]
        [DisplayName("Valid until time (UTC)")]
        [Editor(typeof(NullableDateTimePickerEditor), typeof(UITypeEditor))]
        public DateTime? Expires
        {
            get { return this._expires; }
            set
            {
                this._expires = value;
                this.NotifyPropertyChanged(nameof(this.Expires));
            }
        }

        /// <summary>
        ///     Human readable value of the secret
        /// </summary>
        protected string _value;

        [DisplayName("Value")]
        [Browsable(false)]
        public string Value
        {
            get { return this._value; }
            set
            {
                if (this._value != value)
                {
                    this._value = value;
                    this.NotifyPropertyChanged(nameof(this.Value));
                }
            }
        }

        /// <summary>
        ///     Raw value to store in the vault
        /// </summary>
        [Browsable(false)]
        public string RawValue => this._contentType.ToRawValue(this._value);

        /// <summary>
        ///     Md5 of the raw value
        /// </summary>
        [Browsable(false)]
        public string Md5 => Microsoft.Vault.Library.Utils.CalculateMd5(this.RawValue);

        /// <summary>
        ///     Current SecretKind for this secret object
        ///     Note: NotifyPropertyChanged is NOT called upon set
        /// </summary>
        [Browsable(false)]
        public SecretKind SecretKind { get; set; }

        [Browsable(false)]
        public bool IsNameValid => this.Name == null ? false : this.SecretKind.NameRegex.IsMatch(this.Name);

        [Browsable(false)]
        public bool IsValueValid => this.Value == null ? false : this.SecretKind.ValueRegex.IsMatch(this.Value);

        [Browsable(false)]
        public bool IsExpirationValid => (this.NotBefore ?? DateTime.MinValue) < (this.Expires ?? DateTime.MaxValue)
                                         && (this.Expires ?? DateTime.MaxValue) <= (this.SecretKind.MaxExpiration == TimeSpan.MaxValue ? DateTime.MaxValue : DateTime.UtcNow + this.SecretKind.MaxExpiration);

        protected PropertyObject(ObjectIdentifier identifier, IDictionary<string, string> tags,
            bool? enabled, DateTime? expires, DateTime? notBefore,
            PropertyChangedEventHandler propertyChanged)
        {
            this.Identifier = identifier;
            this.Name = identifier?.Name;

            this.Tags = new ObservableTagItemsCollection();
            if (null != tags)
            {
                foreach (var kvp in tags)
                {
                    this.Tags.Add(new TagItem(kvp));
                }
            }

            this.Tags.SetPropertyChangedEventHandler(propertyChanged);

            this._enabled = enabled;
            this._expires = expires;
            this._notBefore = notBefore;

            this.SecretKind = new SecretKind(); // Default - Custom secret kind

            this.PropertyChanged += propertyChanged;
        }

        public abstract string GetKeyVaultFileExtension();

        /// <summary>
        /// Returns a platform-neutral <see cref="ClipboardPayload"/> describing what should be
        /// placed on the clipboard.  Subclasses override this to add Text and/or FilePath.
        /// </summary>
        public virtual ClipboardPayload GetClipboardValue()
        {
            string filePath = null;
            if (this._contentType.IsCertificate())
            {
                filePath = Path.Combine(Path.GetTempPath(), this.Name + this._contentType.ToExtension());
                this.SaveToFile(filePath);
            }

            return new ClipboardPayload(null, filePath);
        }

        public abstract void SaveToFile(string fullName);

        protected abstract IEnumerable<TagItem> GetValueBasedCustomTags();

        public abstract void PopulateCustomTags();

        public abstract void AddOrUpdateSecretKind(SecretKind sk);

        public abstract string AreCustomTagsValid();

        public abstract void PopulateExpiration();

        public Dictionary<string, string> ToTagsDictionary()
        {
            var result = new Dictionary<string, string>();
            foreach (var tagItem in this.Tags)
            {
                result[tagItem.Name] = tagItem.Value;
            }

            foreach (var tagItem in this.GetValueBasedCustomTags())
            {
                result[tagItem.Name] = tagItem.Value;
            }

            return result;
        }

        public string GetFileName() => this.Name + this._contentType.ToExtension();

        /// <summary>
        /// Copies the object value to the clipboard using the supplied services.
        /// </summary>
        public void CopyToClipboard(IClipboardService clipboardSvc, INotificationService notifySvc, bool showToast)
        {
            var payload = this.GetClipboardValue();
            if (payload.Text is null && payload.FilePath is null)
                return;

            clipboardSvc.SetPayloadAsync(payload).GetAwaiter().GetResult();
            clipboardSvc.SpawnClearClipboardProcess(
                AppSettings.Default.CopyToClipboardTimeToLive,
                Microsoft.Vault.Library.Utils.CalculateMd5(payload.Text ?? string.Empty));

            if (showToast)
                notifySvc.ShowToast($"{(this._contentType.IsCertificate() ? "Certificate" : "Secret")} {this.Name} copied to clipboard");
        }

        public string GetLinkAsInternetShortcut() => $"[InternetShortcut]\nURL={new VaultHttpsUri(this.Identifier.Identifier).VaultLink}";
    }
}
