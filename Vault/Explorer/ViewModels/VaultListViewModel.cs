// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Vault.Explorer.ViewModels
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Reactive.Disposables;
    using System.Reactive.Linq;
    using System.Text.RegularExpressions;
    using DynamicData;
    using DynamicData.Binding;
    using ReactiveUI;

    /// <summary>
    /// Column identifiers for sorting the vault item list.
    /// Mirrors the column order in the WinForms ListView.
    /// </summary>
    public enum VaultListSortColumn
    {
        Name = 0,
        Updated = 1,
        ChangedBy = 2,
        Expires = 3,
    }

    /// <summary>
    /// MVVM replacement for <see cref="Controls.Lists.ListViewSecrets"/>.
    /// Holds all loaded vault items, exposes reactive filtered + sorted collections,
    /// and tracks available tag keys for custom column display.
    /// </summary>
    public sealed class VaultListViewModel : ViewModelBase, IDisposable
    {
        // ── Source ─────────────────────────────────────────────────────────────

        private readonly SourceCache<VaultItemViewModel, string> _source =
            new(vm => vm.Name);

        private readonly CompositeDisposable _disposables = new();

        // ── Reactive state ─────────────────────────────────────────────────────

        private string _searchText = "";
        public string SearchText
        {
            get => _searchText;
            set => this.RaiseAndSetIfChanged(ref _searchText, value);
        }

        private VaultListSortColumn _sortColumn = VaultListSortColumn.Name;
        public VaultListSortColumn SortColumn
        {
            get => _sortColumn;
            set => this.RaiseAndSetIfChanged(ref _sortColumn, value);
        }

        private bool _sortAscending = true;
        public bool SortAscending
        {
            get => _sortAscending;
            set => this.RaiseAndSetIfChanged(ref _sortAscending, value);
        }

        // ── Outputs ────────────────────────────────────────────────────────────

        /// <summary>
        /// Flat, filtered and sorted list for the main DataGrid.
        /// Updated reactively whenever the source, search text, or sort settings change.
        /// </summary>
        public ReadOnlyObservableCollection<VaultItemViewModel> Items { get; }

        /// <summary>
        /// Items currently marked as search results (convenient subset).
        /// </summary>
        public ReadOnlyObservableCollection<VaultItemViewModel> SearchResultItems { get; }

        /// <summary>
        /// Distinct tag keys seen across all loaded items, sorted alphabetically.
        /// Used to drive custom tag columns in the DataGrid.
        /// </summary>
        public ObservableCollection<string> AvailableTagKeys { get; } = new();

        // ── Derived counters ───────────────────────────────────────────────────

        public int TotalCount => _source.Count;

        // ── Constructor ────────────────────────────────────────────────────────

        public VaultListViewModel()
        {
            // Build the sort comparer from reactive properties
            var sortObservable = this.WhenAnyValue(x => x.SortColumn, x => x.SortAscending)
                .Select(t => BuildSortComparer(t.Item1, t.Item2));

            // Build the filter predicate from search text
            var filterObservable = this.WhenAnyValue(x => x.SearchText)
                .Throttle(TimeSpan.FromMilliseconds(150), RxApp.MainThreadScheduler)
                .Select(BuildFilter);

            // Main Items pipeline: filter → sort → bind
            _source.Connect()
                .Filter(filterObservable)
                .Sort(sortObservable)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Bind(out var items)
                .Subscribe()
                .DisposeWith(_disposables);
            Items = items;

            // SearchResultItems pipeline: only items with IsSearchResult == true
            _source.Connect()
                .AutoRefresh(vm => vm.IsSearchResult)
                .Filter(vm => vm.IsSearchResult)
                .Sort(SortExpressionComparer<VaultItemViewModel>.Ascending(x => x.Name))
                .ObserveOn(RxApp.MainThreadScheduler)
                .Bind(out var searchItems)
                .Subscribe()
                .DisposeWith(_disposables);
            SearchResultItems = searchItems;
        }

        // ── Mutation API (called by MainWindowViewModel in Phase 4) ────────────

        /// <summary>
        /// Adds the item, or replaces an existing item with the same name.
        /// Also updates <see cref="AvailableTagKeys"/>.
        /// </summary>
        public void AddOrReplace(VaultItemViewModel item)
        {
            if (_source.Lookup(item.Name).HasValue)
            {
                var old = _source.Lookup(item.Name).Value;
                RemoveTagKeys(old.Tags);
            }

            _source.AddOrUpdate(item);
            AddTagKeys(item.Tags);
        }

        /// <summary>
        /// Removes the item with the given name (if present).
        /// </summary>
        public void Remove(string name)
        {
            var lookup = _source.Lookup(name);
            if (lookup.HasValue)
            {
                RemoveTagKeys(lookup.Value.Tags);
                _source.Remove(name);
            }
        }

        /// <summary>
        /// Clears all items and resets tag key tracking.
        /// </summary>
        public void Clear()
        {
            _source.Clear();
            AvailableTagKeys.Clear();
        }

        // ── Search ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Runs a regex search over all items, marking matches with
        /// <see cref="VaultItemViewModel.IsSearchResult"/> = true.
        /// Returns null on success, or the parse exception on invalid regex.
        /// </summary>
        public Exception? FindItemsWithText(string pattern)
        {
            try
            {
                var regex = new Regex(
                    pattern,
                    RegexOptions.Compiled | RegexOptions.Singleline |
                    RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

                foreach (var vm in _source.Items)
                    vm.IsSearchResult = vm.MatchesRegex(regex);

                return null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }

        /// <summary>Clears all search result marks.</summary>
        public void ClearSearch()
        {
            foreach (var vm in _source.Items)
                vm.IsSearchResult = false;
        }

        // ── Favorites ──────────────────────────────────────────────────────────

        /// <summary>Toggles IsFavorite on each supplied item.</summary>
        public void ToggleFavorites(IEnumerable<VaultItemViewModel> items)
        {
            foreach (var vm in items)
                vm.IsFavorite = !vm.IsFavorite;
        }

        // ── TSV export ─────────────────────────────────────────────────────────

        /// <summary>
        /// Exports the current Items list (or a subset) to a TSV string.
        /// Mirrors ListViewSecrets.ExportToTsv().
        /// </summary>
        public string ExportToTsv(IEnumerable<VaultItemViewModel>? selected = null)
        {
            var rows = selected ?? Items;
            var sb = new System.Text.StringBuilder();

            // Header
            sb.AppendLine("Name\tUpdated\tChanged By\tExpires\tStatus\tValid from (UTC)\tValid until (UTC)\tContent Type");

            foreach (var vm in rows)
            {
                sb.Append(vm.Name).Append('\t');
                sb.Append(vm.Updated?.ToLocalTime().ToString() ?? "(none)").Append('\t');
                sb.Append(vm.ChangedBy).Append('\t');
                sb.Append(Explorer.Common.Utils.ExpirationToString(vm.Expires)).Append('\t');
                sb.Append(vm.Status).Append('\t');
                sb.Append(vm.NotBefore?.ToString() ?? "(none)").Append('\t');
                sb.Append(vm.Expires?.ToString() ?? "(none)").Append('\t');
                sb.AppendLine(Model.ContentTypes.ContentTypeEnumConverter.GetDescription(vm.GetContentType()));
            }

            return sb.ToString();
        }

        // ── IDisposable ────────────────────────────────────────────────────────

        public void Dispose() => _disposables.Dispose();

        // ── Private helpers ────────────────────────────────────────────────────

        private static Func<VaultItemViewModel, bool> BuildFilter(string searchText)
        {
            // Empty search → show everything (no IsSearchResult filtering; that's a separate pipeline)
            if (string.IsNullOrWhiteSpace(searchText))
                return _ => true;

            return vm => vm.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase);
        }

        private static IComparer<VaultItemViewModel> BuildSortComparer(
            VaultListSortColumn col, bool ascending)
        {
            // Primary: effective group order (SearchResults first, then Favorites, etc.)
            // Secondary: the requested column
            SortExpressionComparer<VaultItemViewModel> comparer = col switch
            {
                VaultListSortColumn.Name =>
                    ascending
                        ? SortExpressionComparer<VaultItemViewModel>.Ascending(x => x.Name)
                        : SortExpressionComparer<VaultItemViewModel>.Descending(x => x.Name),
                VaultListSortColumn.Updated =>
                    ascending
                        ? SortExpressionComparer<VaultItemViewModel>.Ascending(x => x.Updated ?? DateTime.MinValue)
                        : SortExpressionComparer<VaultItemViewModel>.Descending(x => x.Updated ?? DateTime.MinValue),
                VaultListSortColumn.ChangedBy =>
                    ascending
                        ? SortExpressionComparer<VaultItemViewModel>.Ascending(x => x.ChangedBy)
                        : SortExpressionComparer<VaultItemViewModel>.Descending(x => x.ChangedBy),
                VaultListSortColumn.Expires =>
                    ascending
                        ? SortExpressionComparer<VaultItemViewModel>.Ascending(x => x.Expires ?? DateTime.MaxValue)
                        : SortExpressionComparer<VaultItemViewModel>.Descending(x => x.Expires ?? DateTime.MaxValue),
                _ =>
                    SortExpressionComparer<VaultItemViewModel>.Ascending(x => x.Name),
            };

            return comparer;
        }

        private void AddTagKeys(IDictionary<string, string>? tags)
        {
            if (tags == null) return;
            foreach (var key in tags.Keys)
                if (!AvailableTagKeys.Contains(key))
                    AvailableTagKeys.Add(key);
        }

        private void RemoveTagKeys(IDictionary<string, string>? tags)
        {
            if (tags == null) return;
            // Only remove a key when no other item still has it
            foreach (var key in tags.Keys)
            {
                bool stillUsed = _source.Items.Any(vm =>
                    vm.Tags != null && vm.Tags.ContainsKey(key));
                if (!stillUsed)
                    AvailableTagKeys.Remove(key);
            }
        }
    }
}
