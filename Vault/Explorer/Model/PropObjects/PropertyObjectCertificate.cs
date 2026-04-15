// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Vault.Explorer.Model.PropObjects
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography.X509Certificates;
    using Azure.Security.KeyVault.Certificates;
    using Microsoft.Vault.Explorer.Controls.MenuItems;
    using Microsoft.Vault.Explorer.Model.Collections;
    using Microsoft.Vault.Explorer.Model.ContentTypes;
    using Microsoft.Vault.Explorer.Model.Files.Secrets;
    using Microsoft.Vault.Library;

    /// <summary>
    ///     Certificate object to edit via PropertyGrid
    /// </summary>
    [DefaultProperty("Certificate")]
    public class PropertyObjectCertificate : PropertyObject
    {
        /// <summary>The vault-stored certificate resource, or null for a brand-new certificate.</summary>
        public readonly KeyVaultCertificate CertificateBundle;

        public readonly CertificatePolicy CertificatePolicy;

        [Category("General")]
        [DisplayName("Certificate")]
        [Description("Displays a system dialog that contains the properties of an X.509 certificate and its associated certificate chain.")]
        public X509Certificate2 Certificate { get; }

        [Category("General")]
        [DisplayName("Thumbprint")]
        public string Thumbprint => this.Certificate.Thumbprint?.ToLowerInvariant();

        [Category("Identifiers")]
        [DisplayName("Certificate Id")]
        public string Id => this.CertificateBundle?.Id?.ToString();

        [Category("Identifiers")]
        [DisplayName("Key Id")]
        public string KeyId => this.CertificateBundle?.KeyId?.ToString();

        [Category("Identifiers")]
        [DisplayName("Secret Id")]
        public string SecretId => this.CertificateBundle?.SecretId?.ToString();

        [Category("Policy")]
        [DisplayName("Enabled")]
        [ReadOnly(true)]
        public bool? PolicyEnabled => this.CertificatePolicy?.Enabled;

        [Category("Policy")]
        [DisplayName("Issuer")]
        [ReadOnly(true)]
        public string Issuer => this.CertificatePolicy?.IssuerName;

        [Category("Policy")]
        [DisplayName("Certificate Type")]
        [ReadOnly(true)]
        public string CertificateType => this.CertificatePolicy?.CertificateType;

        [Category("Policy")]
        [DisplayName("Key Type")]
        [ReadOnly(true)]
        public string KeyType => this.CertificatePolicy?.KeyType?.ToString() ?? "";

        [Category("Policy")]
        [DisplayName("Key Size")]
        [ReadOnly(true)]
        public int? KeySize => this.CertificatePolicy?.KeySize;

        [Category("Policy")]
        [DisplayName("Exportable")]
        [ReadOnly(true)]
        public bool? Exportable => this.CertificatePolicy?.Exportable;

        [Category("Policy")]
        [DisplayName("Reuse Key")]
        [ReadOnly(true)]
        public bool? ReuseKey => this.CertificatePolicy?.ReuseKey;

        [Category("Policy")]
        [DisplayName("Content Type")]
        [ReadOnly(true)]
        public string PolicyContentType => this.CertificatePolicy?.ContentType?.ToString();

        [Category("Policy")]
        [DisplayName("Subject")]
        [ReadOnly(true)]
        public string Subject => this.CertificatePolicy?.Subject;

        private ObservableLifetimeActionsCollection _lifetimeActions;

        [Category("Policy")]
        [DisplayName("Life time actions")]
        [Description("Actions that will be performed by Key Vault over the lifetime of a certificate.")]
        [TypeConverter(typeof(ExpandableCollectionObjectConverter))]
        public ObservableLifetimeActionsCollection LifetimeActions
        {
            get { return this._lifetimeActions; }
            set
            {
                this._lifetimeActions = value;
                if (this.CertificatePolicy?.LifetimeActions != null)
                {
                    this.CertificatePolicy.LifetimeActions.Clear();
                    foreach (var lai in value)
                    {
                        this.CertificatePolicy.LifetimeActions.Add(new LifetimeAction(lai.Type ?? CertificatePolicyAction.AutoRenew)
                        {
                            DaysBeforeExpiry = lai.DaysBeforeExpiry,
                            LifetimePercentage = lai.LifetimePercentage,
                        });
                    }
                }
            }
        }

        public PropertyObjectCertificate(KeyVaultCertificate certificateBundle, CertificatePolicy policy, X509Certificate2 certificate, PropertyChangedEventHandler propertyChanged) :
            base(
                certificateBundle != null
                    ? new ObjectIdentifier(
                        certificateBundle.Name,
                        certificateBundle.Id?.ToString() ?? string.Empty,
                        certificateBundle.Properties.Version ?? string.Empty,
                        certificateBundle.Properties.VaultUri?.ToString() ?? string.Empty)
                    : new ObjectIdentifier(null, null, null, null),
                certificateBundle?.Properties.Tags,
                certificateBundle?.Properties.Enabled,
                certificateBundle?.Properties.ExpiresOn?.UtcDateTime,
                certificateBundle?.Properties.NotBefore?.UtcDateTime,
                propertyChanged)
        {
            this.CertificateBundle = certificateBundle;
            this.CertificatePolicy = policy;
            this.Certificate = certificate;
            this._contentType = ContentType.Pkcs12;
            this._value = certificate.Thumbprint.ToLowerInvariant();
            var olac = new ObservableLifetimeActionsCollection();
            if (this.CertificatePolicy?.LifetimeActions != null)
            {
                foreach (var la in this.CertificatePolicy.LifetimeActions)
                {
                    olac.Add(new LifetimeActionItem
                    {
                        Type = la.Action,
                        DaysBeforeExpiry = la.DaysBeforeExpiry,
                        LifetimePercentage = la.LifetimePercentage,
                    });
                }
            }

            this.LifetimeActions = olac;
            this.LifetimeActions.SetPropertyChangedEventHandler(propertyChanged);
        }

        public override string GetKeyVaultFileExtension() => ContentType.KeyVaultCertificate.ToExtension();

        public override ClipboardPayload GetClipboardValue()
        {
            var basePayload = base.GetClipboardValue();
            return basePayload with { Text = this.Certificate.ToString() };
        }

        public override void SaveToFile(string fullName)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(fullName));
            switch (ContentTypeUtils.FromExtension(Path.GetExtension(fullName)))
            {
                case ContentType.KeyVaultSecret:
                    throw new InvalidOperationException("One can't save key vault certificate as key vault secret");
                case ContentType.KeyVaultCertificate:
                    if (this.CertificateBundle != null)
                    {
                        File.WriteAllText(fullName, new KeyVaultCertificateFile(CertificateFileData.FromCertificate(this.CertificateBundle)).Serialize());
                    }
                    break;
                case ContentType.KeyVaultLink:
                    File.WriteAllText(fullName, this.GetLinkAsInternetShortcut());
                    break;
                case ContentType.Certificate:
                    File.WriteAllBytes(fullName, this.Certificate.Export(X509ContentType.Cert));
                    break;
                case ContentType.Pkcs12:
                    // Password prompt is handled at the ViewModel layer via IDialogService.
                    // Exporting without password here as a fallback; callers that need a password
                    // should use Certificate.Export(X509ContentType.Pkcs12, password) directly.
                    File.WriteAllBytes(fullName, this.Certificate.Export(X509ContentType.Pkcs12));
                    break;
                default:
                    File.WriteAllText(fullName, this.Certificate.ToString());
                    break;
            }
        }

        protected override IEnumerable<TagItem> GetValueBasedCustomTags()
        {
            yield break;
        }

        public override void PopulateCustomTags()
        {
        }

        public override void AddOrUpdateSecretKind(SecretKind sk)
        {
        }

        public override void PopulateExpiration()
        {
        }

        public override string AreCustomTagsValid() => "";
    }
}
