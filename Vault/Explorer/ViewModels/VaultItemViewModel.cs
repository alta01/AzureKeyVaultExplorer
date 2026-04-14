// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Vault.Explorer.ViewModels
{
    using System;
    using System.Collections.Generic;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Vault.Explorer.Common;
    using Microsoft.Vault.Explorer.Controls.Lists.Favorites;
    using Microsoft.Vault.Explorer.Model;
    using Microsoft.Vault.Explorer.Model.ContentTypes;
    using Microsoft.Vault.Explorer.Model.PropObjects;
    using Microsoft.Vault.Library;
    using ReactiveUI;

    /// <summary>
    /// Groups that a vault item can appear in, in priority order (lower value = higher priority).
    /// </summary>
    public enum VaultItemGroup
    {
        SearchResults = 0,
        Favorites = 1,
        Certificates = 2,
        KeyVaultCertificates = 3,
        Secrets = 4,
    }

    /// <summary>
    /// MVVM base for a single vault item (secret or certificate).
    /// Replaces <see cref="Controls.Lists.ListViewItemBase"/> for the Avalonia UI layer.
    /// WinForms code continues to use ListViewItemBase until Phase 5.
    /// </summary>
    public abstract class VaultItemViewModel : ViewModelBase
    {
        // ── Immutable item data ────────────────────────────────────────────────

        public readonly ISession Session;
        public readonly ObjectIdentifier Identifier;
        public readonly IDictionary<string, string>? Tags;
        public readonly bool Enabled;
        public readonly DateTime? Created;
        public readonly DateTime? Updated;
        public readonly DateTime? NotBefore;
        public readonly DateTime? Expires;

        private readonly VaultHttpsUri _vaultUri;

        // ── Computed read-only properties ──────────────────────────────────────

        public string Name => Identifier.Name;

        /// <summary>Full versioned HTTPS identifier URI string.</summary>
        public string Id => _vaultUri.ToString();

        /// <summary>vault:// deep-link, used in Link column and clipboard.</summary>
        public string Link => $"{Globals.ActivationUrl}?{_vaultUri.VaultLink}";

        public string ChangedBy => Microsoft.Vault.Library.Utils.GetChangedBy(Tags);
        public string Md5 => Microsoft.Vault.Library.Utils.GetMd5(Tags);

        public string Status =>
            (Enabled ? "Enabled" : "Disabled") + (Active ? ", Active" : ", Expired");

        /// <summary>True when the current time is within [NotBefore, Expires].</summary>
        public bool Active =>
            DateTime.UtcNow >= (NotBefore ?? DateTime.MinValue) &&
            DateTime.UtcNow <= (Expires ?? DateTime.MaxValue);

        /// <summary>False when item is within the warning window before expiry.</summary>
        public bool AboutToExpire =>
            DateTime.UtcNow + AppSettings.Default.AboutToExpireWarningPeriod <=
            (Expires ?? DateTime.MaxValue);

        // ── Natural group (overridden per item type) ───────────────────────────

        public abstract VaultItemGroup NaturalGroup { get; }

        /// <summary>
        /// Effective display group, accounting for search results and favorites.
        /// </summary>
        public VaultItemGroup EffectiveGroup
        {
            get
            {
                if (_isSearchResult) return VaultItemGroup.SearchResults;
                if (_isFavorite) return VaultItemGroup.Favorites;
                return NaturalGroup;
            }
        }

        // ── Mutable reactive state ─────────────────────────────────────────────

        private bool _isFavorite;
        public bool IsFavorite
        {
            get => _isFavorite;
            set
            {
                if (_isFavorite == value) return;
                this.RaiseAndSetIfChanged(ref _isFavorite, value);
                this.RaisePropertyChanged(nameof(EffectiveGroup));
                if (value)
                    FavoriteSecretUtil.Add(Session.CurrentVaultAlias.Alias, Name);
                else
                    FavoriteSecretUtil.Remove(Session.CurrentVaultAlias.Alias, Name);
            }
        }

        private bool _isSearchResult;
        public bool IsSearchResult
        {
            get => _isSearchResult;
            set
            {
                this.RaiseAndSetIfChanged(ref _isSearchResult, value);
                this.RaisePropertyChanged(nameof(EffectiveGroup));
            }
        }

        // ── Constructor ────────────────────────────────────────────────────────

        protected VaultItemViewModel(
            ISession session,
            ObjectIdentifier identifier,
            IDictionary<string, string>? tags,
            bool? enabled,
            DateTime? created,
            DateTime? updated,
            DateTime? notBefore,
            DateTime? expires)
        {
            Session = session;
            Identifier = identifier;
            Tags = tags;
            Enabled = enabled ?? true;
            Created = created;
            Updated = updated;
            NotBefore = notBefore;
            Expires = expires;

            _vaultUri = new VaultHttpsUri(identifier.Identifier);
            _isFavorite = FavoriteSecretUtil.Contains(session.CurrentVaultAlias.Alias, Name);
        }

        // ── Async vault operations (abstract) ─────────────────────────────────

        public abstract Task<PropertyObject> GetAsync(CancellationToken ct);

        /// <summary>Toggles the Enabled flag and returns a refreshed ViewModel.</summary>
        public abstract Task<VaultItemViewModel> ToggleAsync(CancellationToken ct);

        /// <summary>Resets Expires to +1y and NotBefore to -1h, returns refreshed ViewModel.</summary>
        public abstract Task<VaultItemViewModel> ResetExpirationAsync(CancellationToken ct);

        /// <summary>Deletes the item from the vault and returns this ViewModel.</summary>
        public abstract Task<VaultItemViewModel> DeleteAsync(CancellationToken ct);

        public abstract Task<IEnumerable<object>> GetVersionsAsync(CancellationToken ct);

        public abstract ContentType GetContentType();

        // ── Search helper ──────────────────────────────────────────────────────

        /// <summary>
        /// Returns true if any "Key=Value" property string matches the regex.
        /// Mirrors the logic in ListViewItemBase.Contains().
        /// </summary>
        public bool MatchesRegex(Regex regex)
        {
            if (string.IsNullOrWhiteSpace(regex.ToString())) return false;

            if (TryMatch(regex, "Name", Name)) return true;
            if (TryMatch(regex, "Link", Link)) return true;
            if (TryMatch(regex, "Identifier", Id)) return true;
            if (TryMatch(regex, "Status", Status)) return true;
            if (TryMatch(regex, "Changed by", ChangedBy)) return true;
            if (TryMatch(regex, "Md5", Md5)) return true;
            if (TryMatch(regex, "Enabled", Enabled.ToString())) return true;
            if (TryMatch(regex, "Valid from time (UTC)", NotBefore?.ToString())) return true;
            if (TryMatch(regex, "Valid until time (UTC)", Expires?.ToString())) return true;

            if (Tags != null)
                foreach (var kvp in Tags)
                    if (TryMatch(regex, kvp.Key, kvp.Value)) return true;

            return MatchesExtraProperties(regex);
        }

        /// <summary>Override to add subclass-specific properties to regex search.</summary>
        protected virtual bool MatchesExtraProperties(Regex regex) => false;

        private static bool TryMatch(Regex regex, string key, string? value) =>
            regex.IsMatch($"{key}={value}");
    }
}
