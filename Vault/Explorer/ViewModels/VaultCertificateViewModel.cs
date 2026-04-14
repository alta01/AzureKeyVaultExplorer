// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Vault.Explorer.ViewModels
{
    using System;
    using System.Collections.Generic;
    using System.Security.Cryptography.X509Certificates;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Security.KeyVault.Certificates;
    using Microsoft.Vault.Explorer.Common;
    using Microsoft.Vault.Explorer.Model;
    using Microsoft.Vault.Explorer.Model.ContentTypes;
    using Microsoft.Vault.Explorer.Model.PropObjects;
    using Microsoft.Vault.Library;

    /// <summary>
    /// ViewModel for a native Key Vault certificate (CertificatesGroup).
    /// </summary>
    public sealed class VaultCertificateViewModel : VaultItemViewModel
    {
        public readonly string Thumbprint;

        // ── Constructor ────────────────────────────────────────────────────────

        private VaultCertificateViewModel(
            ISession session,
            ObjectIdentifier identifier,
            string? thumbprint,
            IDictionary<string, string>? tags,
            bool? enabled,
            DateTime? created,
            DateTime? updated,
            DateTime? notBefore,
            DateTime? expires)
            : base(session, identifier, tags, enabled, created, updated, notBefore, expires)
        {
            Thumbprint = thumbprint?.ToLowerInvariant() ?? string.Empty;
        }

        // ── Factory methods ────────────────────────────────────────────────────

        public static VaultCertificateViewModel FromCertificateProperties(
            ISession session, CertificateProperties cp) =>
            new(session,
                new ObjectIdentifier(cp.Name, cp.Id?.ToString() ?? string.Empty, cp.Version ?? string.Empty, cp.VaultUri?.ToString() ?? string.Empty),
                cp.X509Thumbprint != null ? Explorer.Common.Utils.ByteArrayToHex(cp.X509Thumbprint) : null,
                cp.Tags,
                cp.Enabled,
                cp.CreatedOn?.UtcDateTime,
                cp.UpdatedOn?.UtcDateTime,
                cp.NotBefore?.UtcDateTime,
                cp.ExpiresOn?.UtcDateTime);

        public static VaultCertificateViewModel FromKeyVaultCertificate(
            ISession session, KeyVaultCertificate kvc) =>
            new(session,
                new ObjectIdentifier(kvc.Name, kvc.Id?.ToString() ?? string.Empty, kvc.Properties.Version ?? string.Empty, kvc.Properties.VaultUri?.ToString() ?? string.Empty),
                kvc.Properties.X509Thumbprint != null ? Explorer.Common.Utils.ByteArrayToHex(kvc.Properties.X509Thumbprint) : null,
                kvc.Properties.Tags,
                kvc.Properties.Enabled,
                kvc.Properties.CreatedOn?.UtcDateTime,
                kvc.Properties.UpdatedOn?.UtcDateTime,
                kvc.Properties.NotBefore?.UtcDateTime,
                kvc.Properties.ExpiresOn?.UtcDateTime);

        // ── VaultItemViewModel overrides ───────────────────────────────────────

        public override VaultItemGroup NaturalGroup => VaultItemGroup.KeyVaultCertificates;

        public override ContentType GetContentType() => ContentType.Pkcs12;

        protected override bool MatchesExtraProperties(Regex regex) =>
            regex.IsMatch($"Content Type={CertificateContentType.Pkcs12}") ||
            regex.IsMatch($"Thumbprint={Thumbprint}");

        public override async Task<PropertyObject> GetAsync(CancellationToken ct)
        {
            var kvc = await Session.CurrentVault.GetCertificateAsync(Name, null, ct);
            var policy = await Session.CurrentVault.GetCertificatePolicyAsync(Name, ct);
            var cert = await Session.CurrentVault.GetCertificateWithExportableKeysAsync(Name, null, ct);
            return new PropertyObjectCertificate(kvc, policy, cert, null);
        }

        public override async Task<VaultItemViewModel> ToggleAsync(CancellationToken ct)
        {
            var kvc = await Session.CurrentVault.UpdateCertificateAsync(
                Name, null,
                !Enabled,
                Expires.HasValue ? (DateTimeOffset?)Expires.Value : null,
                NotBefore.HasValue ? (DateTimeOffset?)NotBefore.Value : null,
                Tags,
                ct);
            return FromKeyVaultCertificate(Session, kvc);
        }

        public override async Task<VaultItemViewModel> ResetExpirationAsync(CancellationToken ct)
        {
            var newExpires = Expires == null ? (DateTimeOffset?)null : DateTimeOffset.UtcNow.AddYears(1);
            var newNotBefore = NotBefore == null ? (DateTimeOffset?)null : DateTimeOffset.UtcNow.AddHours(-1);
            var kvc = await Session.CurrentVault.UpdateCertificateAsync(
                Name, null,
                Enabled,
                newExpires,
                newNotBefore,
                Tags,
                ct);
            return FromKeyVaultCertificate(Session, kvc);
        }

        public override async Task<VaultItemViewModel> DeleteAsync(CancellationToken ct)
        {
            await Session.CurrentVault.DeleteCertificateAsync(Name, ct);
            return this;
        }

        public override Task<IEnumerable<object>> GetVersionsAsync(CancellationToken ct) =>
            Session.CurrentVault.GetCertificateVersionsAsync(Name, 0, ct);

        // ── New / update helpers (called from dialog ViewModels in Phase 3) ────

        public static async Task<VaultCertificateViewModel> NewAsync(
            ISession session, PropertyObjectCertificate certNew, CancellationToken ct)
        {
            var certCollection = new X509Certificate2Collection();
            certCollection.Add(certNew.Certificate);
            var kvc = await session.CurrentVault.ImportCertificateAsync(
                certNew.Name, certCollection, certNew.CertificatePolicy,
                certNew.Enabled,
                certNew.ToTagsDictionary(),
                ct);
            return FromKeyVaultCertificate(session, kvc);
        }

        public static async Task<VaultCertificateViewModel> UpdateAsync(
            ISession session, PropertyObjectCertificate certNew, CancellationToken ct)
        {
            await session.CurrentVault.UpdateCertificatePolicyAsync(certNew.Name, certNew.CertificatePolicy, ct);
            var kvc = await session.CurrentVault.UpdateCertificateAsync(
                certNew.Name, null,
                certNew.Enabled,
                certNew.Expires.HasValue ? (DateTimeOffset?)certNew.Expires.Value : null,
                certNew.NotBefore.HasValue ? (DateTimeOffset?)certNew.NotBefore.Value : null,
                certNew.ToTagsDictionary(),
                ct);
            return FromKeyVaultCertificate(session, kvc);
        }
    }
}
