// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Vault.Explorer.ViewModels
{
    using System;
    using System.Collections.ObjectModel;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Reactive;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Vault.Library;
    using ReactiveUI;

    // ── Option record types ────────────────────────────────────────────────────

    public sealed record ThemeOption(string Name, string Description);

    public sealed record TimeSpanOption(string Label, TimeSpan? Value)
    {
        // Null Value = "Custom" sentinel
        public bool IsCustom => Value is null;
        public override string ToString() => Label;
    }

    public sealed record ColorOption(string Label, string HexColor)
    {
        public override string ToString() => Label;
    }

    // ── ViewModel ─────────────────────────────────────────────────────────────

    public sealed class SettingsViewModel : ViewModelBase
    {
        // ── Static option lists ────────────────────────────────────────────────

        public static readonly ThemeOption[] ThemeOptions =
        {
            new("Ocean Depths",      "Dark — professional maritime palette (navy + teal)"),
            new("Midnight Galaxy",   "Dark — dramatic cosmic palette (deep purple + blue)"),
            new("Modern Minimalist", "Light — clean contemporary palette (charcoal + gray)"),
            new("Arctic Frost",      "Light — crisp cool palette (steel blue + ice)"),
        };

        public static readonly TimeSpanOption[] ClipboardDelayOptions =
        {
            new("Never",      TimeSpan.Zero),
            new("15 seconds", TimeSpan.FromSeconds(15)),
            new("25 seconds", TimeSpan.FromSeconds(25)),
            new("1 minute",   TimeSpan.FromMinutes(1)),
            new("5 minutes",  TimeSpan.FromMinutes(5)),
        };

        public static readonly TimeSpanOption[] ExpiryWarningOptions =
        {
            new("24 hours",  TimeSpan.FromHours(24)),
            new("3 days",    TimeSpan.FromDays(3)),
            new("1 week",    TimeSpan.FromDays(7)),
            new("2 weeks",   TimeSpan.FromDays(14)),
            new("30 days",   TimeSpan.FromDays(30)),
            new("Custom",    null),
        };

        public static readonly ColorOption[] StatusColorOptions =
        {
            new("Red",       "#E53935"),
            new("Crimson",   "#C62828"),
            new("Orange",    "#FB8C00"),
            new("Gold",      "#F9A825"),
            new("Yellow",    "#FDD835"),
            new("Green",     "#43A047"),
            new("Teal",      "#00897B"),
            new("Blue",      "#1E88E5"),
            new("Purple",    "#8E24AA"),
            new("Gray",      "#757575"),
            new("Dark Gray", "#424242"),
        };

        // ── Bindable settings copy ─────────────────────────────────────────────

        /// <summary>Live editable copy — written back to AppSettings.Default on Save.</summary>
        public AppSettings EditCopy { get; } = CloneSettings();

        // ── Dropdown selections ────────────────────────────────────────────────

        private ThemeOption _selectedTheme;
        public ThemeOption SelectedTheme
        {
            get => _selectedTheme;
            set
            {
                this.RaiseAndSetIfChanged(ref _selectedTheme, value);
                if (value == null) return;
                EditCopy.Theme = value.Name;
                if (!_constructing) IsDirty = true;
                App.ApplyTheme(value.Name);
            }
        }

        private TimeSpanOption _selectedClipboardDelay;
        public TimeSpanOption SelectedClipboardDelay
        {
            get => _selectedClipboardDelay;
            set
            {
                this.RaiseAndSetIfChanged(ref _selectedClipboardDelay, value);
                if (value?.Value is TimeSpan ts)
                {
                    EditCopy.CopyToClipboardTimeToLive = ts;
                    if (!_constructing) IsDirty = true;
                }
            }
        }

        private TimeSpanOption _selectedExpiryWarning;
        public TimeSpanOption SelectedExpiryWarning
        {
            get => _selectedExpiryWarning;
            set
            {
                this.RaiseAndSetIfChanged(ref _selectedExpiryWarning, value);
                if (value?.Value is TimeSpan ts)
                {
                    EditCopy.AboutToExpireWarningPeriod = ts;
                    if (!_constructing) IsDirty = true;
                }
                this.RaisePropertyChanged(nameof(IsCustomExpiry));
            }
        }

        public bool IsCustomExpiry => _selectedExpiryWarning?.IsCustom == true;

        private int _customExpiryDays = 14;
        public int CustomExpiryDays
        {
            get => _customExpiryDays;
            set
            {
                var clamped = Math.Clamp(value, 1, 90);
                this.RaiseAndSetIfChanged(ref _customExpiryDays, clamped);
                if (IsCustomExpiry)
                {
                    EditCopy.AboutToExpireWarningPeriod = TimeSpan.FromDays(clamped);
                    IsDirty = true;
                }
            }
        }

        private ColorOption _selectedExpiredColor;
        public ColorOption SelectedExpiredColor
        {
            get => _selectedExpiredColor;
            set
            {
                this.RaiseAndSetIfChanged(ref _selectedExpiredColor, value);
                if (value == null) return;
                EditCopy.ExpiredItemColor = value.Label;
                if (!_constructing) IsDirty = true;
            }
        }

        private ColorOption _selectedExpiringColor;
        public ColorOption SelectedExpiringColor
        {
            get => _selectedExpiringColor;
            set
            {
                this.RaiseAndSetIfChanged(ref _selectedExpiringColor, value);
                if (value == null) return;
                EditCopy.AboutToExpireItemColor = value.Label;
                if (!_constructing) IsDirty = true;
            }
        }

        private ColorOption _selectedDisabledColor;
        public ColorOption SelectedDisabledColor
        {
            get => _selectedDisabledColor;
            set
            {
                this.RaiseAndSetIfChanged(ref _selectedDisabledColor, value);
                if (value == null) return;
                EditCopy.DisabledItemColor = value.Label;
                if (!_constructing) IsDirty = true;
            }
        }

        // ── Accounts list ──────────────────────────────────────────────────────

        public ObservableCollection<string> AccountNames { get; }

        private string _newAccountName = "";
        public string NewAccountName
        {
            get => _newAccountName;
            set => this.RaiseAndSetIfChanged(ref _newAccountName, value);
        }

        // ── Read-only info ─────────────────────────────────────────────────────

        public string VersionsInfo { get; } = BuildVersionsInfo();
        public string LicenseText { get; } = ReadLicenseText();

        /// <summary>Static so it can be referenced from AXAML x:Static.</summary>
        public static string SettingsFilePathDisplay => AppSettings.SettingsFilePath;

        // ── Commands ───────────────────────────────────────────────────────────

        public ReactiveCommand<Unit, Unit> SaveCommand { get; }
        public ReactiveCommand<Unit, Unit> OpenGitHubCommand { get; }
        public ReactiveCommand<Unit, Unit> OpenFeedbackCommand { get; }
        public ReactiveCommand<Unit, Unit> OpenInstallFolderCommand { get; }
        public ReactiveCommand<Unit, Unit> OpenSettingsFolderCommand { get; }
        public ReactiveCommand<Unit, Unit> ClearTokenCacheCommand { get; }
        public ReactiveCommand<Unit, Unit> AddAccountCommand { get; }
        public ReactiveCommand<string, Unit> RemoveAccountCommand { get; }

        // ── Dirty tracking ─────────────────────────────────────────────────────

        private bool _isDirty;
        public bool IsDirty
        {
            get => _isDirty;
            set => this.RaiseAndSetIfChanged(ref _isDirty, value);
        }

        // ── Construction guard (prevents binding initialisation from dirtying state) ──
        private bool _constructing = true;

        // ── Constructor ────────────────────────────────────────────────────────

        public SettingsViewModel()
        {
            var src = AppSettings.Default;

            // Initialise dropdown selections from saved settings
            _selectedTheme = ThemeOptions.FirstOrDefault(t => t.Name == src.Theme)
                             ?? ThemeOptions[0];

            _selectedClipboardDelay = ClipboardDelayOptions
                .FirstOrDefault(o => o.Value == src.CopyToClipboardTimeToLive)
                ?? ClipboardDelayOptions.First(o => o.Label == "1 minute");

            var expiryMatch = ExpiryWarningOptions
                .FirstOrDefault(o => o.Value == src.AboutToExpireWarningPeriod);
            if (expiryMatch != null)
            {
                _selectedExpiryWarning = expiryMatch;
            }
            else
            {
                _selectedExpiryWarning = ExpiryWarningOptions.First(o => o.IsCustom);
                _customExpiryDays = (int)src.AboutToExpireWarningPeriod.TotalDays;
            }

            _selectedExpiredColor = StatusColorOptions
                .FirstOrDefault(c => string.Equals(c.Label, src.ExpiredItemColor, StringComparison.OrdinalIgnoreCase))
                ?? StatusColorOptions.First(c => c.Label == "Red");

            _selectedExpiringColor = StatusColorOptions
                .FirstOrDefault(c => string.Equals(c.Label, src.AboutToExpireItemColor, StringComparison.OrdinalIgnoreCase))
                ?? StatusColorOptions.First(c => c.Label == "Orange");

            _selectedDisabledColor = StatusColorOptions
                .FirstOrDefault(c => string.Equals(c.Label, src.DisabledItemColor, StringComparison.OrdinalIgnoreCase))
                ?? StatusColorOptions.First(c => c.Label == "Gray");

            AccountNames = new ObservableCollection<string>(src.UserAccountNamesList);

            var canSave = this.WhenAnyValue(x => x.IsDirty);
            SaveCommand = ReactiveCommand.Create(Save, canSave);
            SaveCommand.ThrownExceptions
                .Subscribe(async ex =>
                {
                    var dialogs = App.Services.GetRequiredService<Services.IDialogService>();
                    await dialogs.ShowErrorAsync("Save failed", ex.Message, ex);
                });

            OpenGitHubCommand    = ReactiveCommand.Create(() => OpenUrl(Globals.GitHubUrl));
            OpenFeedbackCommand  = ReactiveCommand.Create(() => OpenUrl(Globals.GitHubIssuesUrl));
            OpenInstallFolderCommand  = ReactiveCommand.Create(() => OpenFolder(AppContext.BaseDirectory));
            OpenSettingsFolderCommand = ReactiveCommand.Create(
                () => OpenFolder(Path.GetDirectoryName(AppSettings.SettingsFilePath)!));

            ClearTokenCacheCommand = ReactiveCommand.CreateFromTask(ClearTokenCacheAsync);

            var canAddAccount = this.WhenAnyValue(
                x => x.NewAccountName,
                name => !string.IsNullOrWhiteSpace(name) && !AccountNames.Contains(name.Trim()));
            AddAccountCommand = ReactiveCommand.Create(AddAccount, canAddAccount);
            RemoveAccountCommand = ReactiveCommand.Create<string>(RemoveAccount);

            // Initialisation complete — bindings can now mark the form dirty
            _constructing = false;
        }

        // ── Private helpers ────────────────────────────────────────────────────

        private void AddAccount()
        {
            var name = NewAccountName.Trim();
            if (string.IsNullOrEmpty(name) || AccountNames.Contains(name)) return;
            AccountNames.Add(name);
            NewAccountName = "";
            SyncAccountsToEditCopy();
        }

        private void RemoveAccount(string name)
        {
            AccountNames.Remove(name);
            SyncAccountsToEditCopy();
        }

        private void SyncAccountsToEditCopy()
        {
            EditCopy.UserAccountNames = string.Join("\n", AccountNames);
            IsDirty = true;
        }

        private async Task ClearTokenCacheAsync()
        {
            FileTokenCache.ClearAllFileTokenCaches();
            var dialogs = App.Services.GetRequiredService<Services.IDialogService>();
            await dialogs.ShowMessageAsync(
                "Token Cache Cleared",
                "All cached authentication tokens have been removed.\n\nYou will be prompted to sign in again when you next connect to a vault.");
        }

        private void Save()
        {
            // Validate config file paths before persisting
            ValidatePathField(EditCopy.JsonConfigurationFilesRoot, "JSON configuration files root");
            ValidatePathField(EditCopy.VaultsJsonFileLocation, "Vaults JSON file location");

            var target = AppSettings.Default;
            target.CopyFrom(EditCopy);
            target.UserAccountNames = string.Join("\n", AccountNames);
            target.Save();
            IsDirty = false;
        }

        private static void ValidatePathField(string path, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(path)) return;  // empty = use default
            // Reject path traversal regardless of whether path is absolute or relative
            if (path.Contains(".."))
                throw new ArgumentException($"{fieldName}: path traversal ('..') is not allowed.");
            // Only require absolute path when the value contains a directory separator
            // (plain filenames like "Vaults.json" are valid relative-to-root defaults)
            if ((path.Contains('/') || path.Contains('\\')) && !System.IO.Path.IsPathRooted(path))
                throw new ArgumentException($"{fieldName}: must be an absolute path when a directory is specified.");
        }

        private static AppSettings CloneSettings()
        {
            var src = AppSettings.Default;
            return new AppSettings
            {
                Theme                       = src.Theme,
                CopyToClipboardTimeToLive   = src.CopyToClipboardTimeToLive,
                AboutToExpireWarningPeriod  = src.AboutToExpireWarningPeriod,
                AboutToExpireItemColor      = src.AboutToExpireItemColor,
                ExpiredItemColor            = src.ExpiredItemColor,
                DisabledItemColor           = src.DisabledItemColor,
                JsonConfigurationFilesRoot  = src.JsonConfigurationFilesRoot,
                VaultsJsonFileLocation      = src.VaultsJsonFileLocation,
                VaultAliasesJsonFileLocation = src.VaultAliasesJsonFileLocation,
                SecretKindsJsonFileLocation = src.SecretKindsJsonFileLocation,
                CustomTagsJsonFileLocation  = src.CustomTagsJsonFileLocation,
                UserAccountNames            = src.UserAccountNames,
                LastUsedVaultAlias          = src.LastUsedVaultAlias,
            };
        }

        private static string BuildVersionsInfo()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"{Globals.AppName} version: {typeof(SettingsViewModel).Assembly.GetName().Version} ({(Environment.Is64BitProcess ? "x64" : "x86")})");
            sb.AppendLine($".NET version: {Environment.Version}");
            AppendAssemblyVersion(sb, "Azure.Security.KeyVault.Secrets");
            AppendAssemblyVersion(sb, "Azure.ResourceManager.KeyVault");
            return sb.ToString().TrimEnd();
        }

        private static void AppendAssemblyVersion(StringBuilder sb, string assemblyName)
        {
            var asm = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == assemblyName);
            string version = asm?.GetName().Version?.ToString() ?? "not loaded";
            sb.AppendLine($"{assemblyName}: {version}");
        }

        private static string ReadLicenseText()
        {
            string path = Path.Combine(AppContext.BaseDirectory, "License.txt");
            return File.Exists(path) ? File.ReadAllText(path) : "(license file not found)";
        }

        private static void OpenUrl(string url)
        {
            try { Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true }); }
            catch { /* best-effort */ }
        }

        private static void OpenFolder(string folder)
        {
            try
            {
                Directory.CreateDirectory(folder);
                Process.Start(new ProcessStartInfo { FileName = folder, UseShellExecute = true });
            }
            catch { /* best-effort */ }
        }
    }
}
