// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Vault.Explorer.Controls.Lists
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows.Forms;
    using Azure.Security.KeyVault.Secrets;
    using Microsoft.Vault.Explorer.Common;
    using Microsoft.Vault.Explorer.Dialogs.Secrets;
    using Microsoft.Vault.Explorer.Model;
    using Microsoft.Vault.Explorer.Model.ContentTypes;
    using Microsoft.Vault.Explorer.Model.PropObjects;
    using Microsoft.Vault.Library;

    /// <summary>
    ///     Secret list view item which also presents itself nicely to PropertyGrid
    /// </summary>
    public class ListViewItemSecret : ListViewItemBase
    {
        public readonly string ContentTypeStr;
        public readonly ContentType ContentType;

        private ListViewItemSecret(ISession session, ObjectIdentifier identifier, string contentTypeStr, IDictionary<string, string> tags, bool? enabled, DateTime? created, DateTime? updated, DateTime? notBefore, DateTime? expires) :
            base(session, ContentTypeEnumConverter.GetValue(contentTypeStr).IsCertificate() ? CertificatesGroup : SecretsGroup,
                identifier, tags, enabled, created, updated, notBefore, expires)
        {
            this.ContentTypeStr = contentTypeStr;
            this.ContentType = ContentTypeEnumConverter.GetValue(contentTypeStr);
        }

        public ListViewItemSecret(ISession session, SecretProperties sp) : this(
            session,
            new ObjectIdentifier(sp.Name, sp.Id?.ToString() ?? string.Empty, sp.Version ?? string.Empty, sp.VaultUri?.ToString() ?? string.Empty),
            sp.ContentType,
            sp.Tags,
            sp.Enabled,
            sp.CreatedOn?.UtcDateTime,
            sp.UpdatedOn?.UtcDateTime,
            sp.NotBefore?.UtcDateTime,
            sp.ExpiresOn?.UtcDateTime)
        {
        }

        public ListViewItemSecret(ISession session, KeyVaultSecret s) : this(
            session,
            new ObjectIdentifier(s.Name, s.Id?.ToString() ?? string.Empty, s.Properties.Version ?? string.Empty, s.Properties.VaultUri?.ToString() ?? string.Empty),
            s.Properties.ContentType,
            s.Properties.Tags,
            s.Properties.Enabled,
            s.Properties.CreatedOn?.UtcDateTime,
            s.Properties.UpdatedOn?.UtcDateTime,
            s.Properties.NotBefore?.UtcDateTime,
            s.Properties.ExpiresOn?.UtcDateTime)
        {
        }

        protected override IEnumerable<PropertyDescriptor> GetCustomProperties()
        {
            yield return new ReadOnlyPropertyDescriptor("Content Type", this.ContentTypeStr);
        }

        public override ContentType GetContentType() => this.ContentType;

        public override async Task<PropertyObject> GetAsync(CancellationToken cancellationToken)
        {
            var s = await this.Session.CurrentVault.GetSecretAsync(this.Name, null, cancellationToken);
            return new PropertyObjectSecret(s, null);
        }

        public override async Task<ListViewItemBase> ToggleAsync(CancellationToken cancellationToken)
        {
            var sp = await this.Session.CurrentVault.UpdateSecretAsync(
                this.Name, null,
                new Dictionary<string, string>(this.Tags),
                this.ContentTypeStr,
                !this.Enabled,
                this.Expires.HasValue ? (DateTimeOffset?)this.Expires.Value : null,
                this.NotBefore.HasValue ? (DateTimeOffset?)this.NotBefore.Value : null,
                cancellationToken);
            return new ListViewItemSecret(this.Session, sp);
        }

        public override async Task<ListViewItemBase> ResetExpirationAsync(CancellationToken cancellationToken)
        {
            DateTimeOffset? newExpires = this.Expires == null ? null : (DateTimeOffset?)DateTimeOffset.UtcNow.AddYears(1);
            DateTimeOffset? newNotBefore = this.NotBefore == null ? null : (DateTimeOffset?)DateTimeOffset.UtcNow.AddHours(-1);
            var sp = await this.Session.CurrentVault.UpdateSecretAsync(
                this.Name, null,
                new Dictionary<string, string>(this.Tags),
                this.ContentTypeStr,
                this.Enabled,
                newExpires,
                newNotBefore,
                cancellationToken);
            return new ListViewItemSecret(this.Session, sp);
        }

        public override async Task<ListViewItemBase> DeleteAsync(CancellationToken cancellationToken)
        {
            await this.Session.CurrentVault.DeleteSecretAsync(this.Name, cancellationToken);
            return this;
        }

        public override async Task<IEnumerable<object>> GetVersionsAsync(CancellationToken cancellationToken)
        {
            return await this.Session.CurrentVault.GetSecretVersionsAsync(this.Name, 0, cancellationToken);
        }

        public override Form GetEditDialog(string name, IEnumerable<object> versions)
        {
            return new SecretDialog(this.Session, name, versions.Cast<SecretProperties>());
        }

        private static async Task<ListViewItemSecret> NewOrUpdateAsync(ISession session, object originalObject, PropertyObject newObject, CancellationToken cancellationToken)
        {
            KeyVaultSecret sOriginal = (KeyVaultSecret)originalObject;
            PropertyObjectSecret posNew = (PropertyObjectSecret)newObject;

            DateTimeOffset? expires = posNew.Expires.HasValue ? (DateTimeOffset?)posNew.Expires.Value : null;
            DateTimeOffset? notBefore = posNew.NotBefore.HasValue ? (DateTimeOffset?)posNew.NotBefore.Value : null;

            // New secret, rename, or new value
            if (sOriginal == null || sOriginal.Name != posNew.Name || sOriginal.Value != posNew.RawValue)
            {
                var s = await session.CurrentVault.SetSecretAsync(
                    posNew.Name, posNew.RawValue, posNew.ToTagsDictionary(),
                    ContentTypeEnumConverter.GetDescription(posNew.ContentType),
                    posNew.Enabled, expires, notBefore, cancellationToken);

                string oldSecretName = sOriginal?.Name;
                if (oldSecretName != null && oldSecretName != posNew.Name)
                {
                    await session.CurrentVault.DeleteSecretAsync(oldSecretName, cancellationToken);
                }

                return new ListViewItemSecret(session, s);
            }
            else // Same secret name and value — update metadata only
            {
                var sp = await session.CurrentVault.UpdateSecretAsync(
                    posNew.Name, null, posNew.ToTagsDictionary(),
                    ContentTypeEnumConverter.GetDescription(posNew.ContentType),
                    posNew.Enabled, expires, notBefore, cancellationToken);

                return new ListViewItemSecret(session, sp);
            }
        }

        public override async Task<ListViewItemBase> UpdateAsync(object originalObject, PropertyObject newObject, CancellationToken cancellationToken)
        {
            return await NewOrUpdateAsync(this.Session, originalObject, newObject, cancellationToken);
        }

        public static Task<ListViewItemSecret> NewAsync(ISession session, PropertyObject newObject, CancellationToken cancellationToken)
        {
            return NewOrUpdateAsync(session, null, newObject, cancellationToken);
        }
    }
}
