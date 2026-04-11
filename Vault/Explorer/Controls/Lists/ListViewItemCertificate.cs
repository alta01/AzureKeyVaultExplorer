// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Vault.Explorer.Controls.Lists
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Linq;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows.Forms;
    using Azure.Security.KeyVault.Certificates;
    using Microsoft.Vault.Explorer.Common;
    using Microsoft.Vault.Explorer.Dialogs.Certificates;
    using Microsoft.Vault.Explorer.Model;
    using Microsoft.Vault.Explorer.Model.ContentTypes;
    using Microsoft.Vault.Explorer.Model.PropObjects;
    using Microsoft.Vault.Library;

    /// <summary>
    ///     Key Vault Certificate list view item which also presents itself nicely to PropertyGrid
    /// </summary>
    public class ListViewItemCertificate : ListViewItemBase
    {
        public readonly string Thumbprint;

        private ListViewItemCertificate(ISession session, ObjectIdentifier identifier, string thumbprint, IDictionary<string, string> tags, bool? enabled, DateTime? created, DateTime? updated, DateTime? notBefore, DateTime? expires) :
            base(session, KeyVaultCertificatesGroup, identifier, tags, enabled, created, updated, notBefore, expires)
        {
            this.Thumbprint = thumbprint?.ToLowerInvariant();
        }

        public ListViewItemCertificate(ISession session, CertificateProperties cp) : this(
            session,
            new ObjectIdentifier(cp.Name, cp.Id?.ToString() ?? string.Empty, cp.Version ?? string.Empty, cp.VaultUri?.ToString() ?? string.Empty),
            Utils.ByteArrayToHex(cp.X509Thumbprint),
            cp.Tags,
            cp.Enabled,
            cp.CreatedOn?.UtcDateTime,
            cp.UpdatedOn?.UtcDateTime,
            cp.NotBefore?.UtcDateTime,
            cp.ExpiresOn?.UtcDateTime)
        {
        }

        public ListViewItemCertificate(ISession session, KeyVaultCertificate kvc) : this(
            session,
            new ObjectIdentifier(kvc.Name, kvc.Id?.ToString() ?? string.Empty, kvc.Properties.Version ?? string.Empty, kvc.Properties.VaultUri?.ToString() ?? string.Empty),
            Utils.ByteArrayToHex(kvc.Properties.X509Thumbprint),
            kvc.Properties.Tags,
            kvc.Properties.Enabled,
            kvc.Properties.CreatedOn?.UtcDateTime,
            kvc.Properties.UpdatedOn?.UtcDateTime,
            kvc.Properties.NotBefore?.UtcDateTime,
            kvc.Properties.ExpiresOn?.UtcDateTime)
        {
        }

        protected override IEnumerable<PropertyDescriptor> GetCustomProperties()
        {
            yield return new ReadOnlyPropertyDescriptor("Content Type", CertificateContentType.Pkcs12.ToString());
            yield return new ReadOnlyPropertyDescriptor("Thumbprint", this.Thumbprint);
        }

        public override ContentType GetContentType() => ContentType.Pkcs12;

        public override async Task<PropertyObject> GetAsync(CancellationToken cancellationToken)
        {
            var kvc = await this.Session.CurrentVault.GetCertificateAsync(this.Name, null, cancellationToken);
            var policy = await this.Session.CurrentVault.GetCertificatePolicyAsync(this.Name, cancellationToken);
            var cert = await this.Session.CurrentVault.GetCertificateWithExportableKeysAsync(this.Name, null, cancellationToken);
            return new PropertyObjectCertificate(kvc, policy, cert, null);
        }

        public override async Task<ListViewItemBase> ToggleAsync(CancellationToken cancellationToken)
        {
            var kvc = await this.Session.CurrentVault.UpdateCertificateAsync(
                this.Name, null,
                !this.Enabled,
                this.Expires.HasValue ? (DateTimeOffset?)this.Expires.Value : null,
                this.NotBefore.HasValue ? (DateTimeOffset?)this.NotBefore.Value : null,
                this.Tags,
                cancellationToken);
            return new ListViewItemCertificate(this.Session, kvc);
        }

        public override async Task<ListViewItemBase> ResetExpirationAsync(CancellationToken cancellationToken)
        {
            DateTimeOffset? newExpires = this.Expires == null ? null : (DateTimeOffset?)DateTimeOffset.UtcNow.AddYears(1);
            DateTimeOffset? newNotBefore = this.NotBefore == null ? null : (DateTimeOffset?)DateTimeOffset.UtcNow.AddHours(-1);
            var kvc = await this.Session.CurrentVault.UpdateCertificateAsync(
                this.Name, null,
                this.Enabled,
                newExpires,
                newNotBefore,
                this.Tags,
                cancellationToken);
            return new ListViewItemCertificate(this.Session, kvc);
        }

        public override async Task<ListViewItemBase> DeleteAsync(CancellationToken cancellationToken)
        {
            await this.Session.CurrentVault.DeleteCertificateAsync(this.Name, cancellationToken);
            return this;
        }

        public override async Task<IEnumerable<object>> GetVersionsAsync(CancellationToken cancellationToken)
        {
            return await this.Session.CurrentVault.GetCertificateVersionsAsync(this.Name, 0, cancellationToken);
        }

        public override Form GetEditDialog(string name, IEnumerable<object> versions)
        {
            return new CertificateDialog(this.Session, name, versions.Cast<CertificateProperties>());
        }

        public override async Task<ListViewItemBase> UpdateAsync(object originalObject, PropertyObject newObject, CancellationToken cancellationToken)
        {
            PropertyObjectCertificate certNew = (PropertyObjectCertificate)newObject;
            await this.Session.CurrentVault.UpdateCertificatePolicyAsync(certNew.Name, certNew.CertificatePolicy, cancellationToken);
            var kvc = await this.Session.CurrentVault.UpdateCertificateAsync(
                certNew.Name, null,
                certNew.Enabled,
                certNew.Expires.HasValue ? (DateTimeOffset?)certNew.Expires.Value : null,
                certNew.NotBefore.HasValue ? (DateTimeOffset?)certNew.NotBefore.Value : null,
                certNew.ToTagsDictionary(),
                cancellationToken);
            return new ListViewItemCertificate(this.Session, kvc);
        }

        public static async Task<ListViewItemCertificate> NewAsync(ISession session, PropertyObject newObject, CancellationToken cancellationToken)
        {
            PropertyObjectCertificate certNew = (PropertyObjectCertificate)newObject;
            var certCollection = new X509Certificate2Collection();
            certCollection.Add(certNew.Certificate);
            var kvc = await session.CurrentVault.ImportCertificateAsync(
                certNew.Name, certCollection, certNew.CertificatePolicy,
                certNew.Enabled,
                certNew.ToTagsDictionary(),
                cancellationToken);
            return new ListViewItemCertificate(session, kvc);
        }
    }
}
