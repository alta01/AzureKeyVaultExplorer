// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Vault.Explorer.ViewModels
{
    using System;
    using System.Collections.Generic;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Security.KeyVault.Secrets;
    using Microsoft.Vault.Explorer.Common;
    using Microsoft.Vault.Explorer.Model;
    using Microsoft.Vault.Explorer.Model.ContentTypes;
    using Microsoft.Vault.Explorer.Model.PropObjects;
    using Microsoft.Vault.Library;

    /// <summary>
    /// ViewModel for a Key Vault secret (including certificate-type secrets stored as secrets).
    /// </summary>
    public sealed class VaultSecretViewModel : VaultItemViewModel
    {
        public readonly string ContentTypeStr;
        public readonly ContentType ContentType;

        // ── Constructor ────────────────────────────────────────────────────────

        private VaultSecretViewModel(
            ISession session,
            ObjectIdentifier identifier,
            string? contentTypeStr,
            IDictionary<string, string>? tags,
            bool? enabled,
            DateTime? created,
            DateTime? updated,
            DateTime? notBefore,
            DateTime? expires)
            : base(session, identifier, tags, enabled, created, updated, notBefore, expires)
        {
            ContentTypeStr = contentTypeStr ?? string.Empty;
            ContentType = ContentTypeEnumConverter.GetValue(ContentTypeStr);
        }

        // ── Factory methods ────────────────────────────────────────────────────

        public static VaultSecretViewModel FromSecretProperties(ISession session, SecretProperties sp) =>
            new(session,
                new ObjectIdentifier(sp.Name, sp.Id?.ToString() ?? string.Empty, sp.Version ?? string.Empty, sp.VaultUri?.ToString() ?? string.Empty),
                sp.ContentType,
                sp.Tags,
                sp.Enabled,
                sp.CreatedOn?.UtcDateTime,
                sp.UpdatedOn?.UtcDateTime,
                sp.NotBefore?.UtcDateTime,
                sp.ExpiresOn?.UtcDateTime);

        public static VaultSecretViewModel FromKeyVaultSecret(ISession session, KeyVaultSecret s) =>
            new(session,
                new ObjectIdentifier(s.Name, s.Id?.ToString() ?? string.Empty, s.Properties.Version ?? string.Empty, s.Properties.VaultUri?.ToString() ?? string.Empty),
                s.Properties.ContentType,
                s.Properties.Tags,
                s.Properties.Enabled,
                s.Properties.CreatedOn?.UtcDateTime,
                s.Properties.UpdatedOn?.UtcDateTime,
                s.Properties.NotBefore?.UtcDateTime,
                s.Properties.ExpiresOn?.UtcDateTime);

        // ── VaultItemViewModel overrides ───────────────────────────────────────

        public override VaultItemGroup NaturalGroup =>
            ContentType.IsCertificate() ? VaultItemGroup.Certificates : VaultItemGroup.Secrets;

        public override ContentType GetContentType() => ContentType;

        protected override bool MatchesExtraProperties(Regex regex) =>
            regex.IsMatch($"Content Type={ContentTypeStr}");

        public override async Task<PropertyObject> GetAsync(CancellationToken ct)
        {
            var s = await Session.CurrentVault.GetSecretAsync(Name, null, ct);
            return new PropertyObjectSecret(s, null);
        }

        public override async Task<VaultItemViewModel> ToggleAsync(CancellationToken ct)
        {
            var sp = await Session.CurrentVault.UpdateSecretAsync(
                Name, null,
                new Dictionary<string, string>(Tags ?? new Dictionary<string, string>()),
                ContentTypeStr,
                !Enabled,
                Expires.HasValue ? (DateTimeOffset?)Expires.Value : null,
                NotBefore.HasValue ? (DateTimeOffset?)NotBefore.Value : null,
                ct);
            return FromSecretProperties(Session, sp);
        }

        public override async Task<VaultItemViewModel> ResetExpirationAsync(CancellationToken ct)
        {
            var newExpires = Expires == null ? (DateTimeOffset?)null : DateTimeOffset.UtcNow.AddYears(1);
            var newNotBefore = NotBefore == null ? (DateTimeOffset?)null : DateTimeOffset.UtcNow.AddHours(-1);
            var sp = await Session.CurrentVault.UpdateSecretAsync(
                Name, null,
                new Dictionary<string, string>(Tags ?? new Dictionary<string, string>()),
                ContentTypeStr,
                Enabled,
                newExpires,
                newNotBefore,
                ct);
            return FromSecretProperties(Session, sp);
        }

        public override async Task<VaultItemViewModel> DeleteAsync(CancellationToken ct)
        {
            await Session.CurrentVault.DeleteSecretAsync(Name, ct);
            return this;
        }

        public override Task<IEnumerable<object>> GetVersionsAsync(CancellationToken ct) =>
            Session.CurrentVault.GetSecretVersionsAsync(Name, 0, ct);

        // ── New / update helpers (called from dialog ViewModels in Phase 3) ────

        /// <summary>Creates a new secret or renames/updates an existing one.</summary>
        public static async Task<VaultSecretViewModel> SaveAsync(
            ISession session,
            KeyVaultSecret? original,
            PropertyObjectSecret newPo,
            CancellationToken ct)
        {
            DateTimeOffset? expires = newPo.Expires.HasValue ? (DateTimeOffset?)newPo.Expires.Value : null;
            DateTimeOffset? notBefore = newPo.NotBefore.HasValue ? (DateTimeOffset?)newPo.NotBefore.Value : null;

            if (original == null || original.Name != newPo.Name || original.Value != newPo.RawValue)
            {
                var s = await session.CurrentVault.SetSecretAsync(
                    newPo.Name, newPo.RawValue, newPo.ToTagsDictionary(),
                    ContentTypeEnumConverter.GetDescription(newPo.ContentType),
                    newPo.Enabled, expires, notBefore, ct);

                if (original?.Name != null && original.Name != newPo.Name)
                    await session.CurrentVault.DeleteSecretAsync(original.Name, ct);

                return FromKeyVaultSecret(session, s);
            }
            else
            {
                var sp = await session.CurrentVault.UpdateSecretAsync(
                    newPo.Name, null, newPo.ToTagsDictionary(),
                    ContentTypeEnumConverter.GetDescription(newPo.ContentType),
                    newPo.Enabled, expires, notBefore, ct);
                return FromSecretProperties(session, sp);
            }
        }
    }
}
