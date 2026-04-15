// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Vault.Explorer.ViewModels
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Reactive;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Vault.Library;
    using ReactiveUI;

    public sealed class SettingsViewModel : ViewModelBase
    {
        // ── Bindable settings ─────────────────────────────────────────────────

        /// <summary>Live editable copy of settings; written back on Save.</summary>
        public AppSettings EditCopy { get; } = CloneSettings();

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

        // ── Dirty tracking ─────────────────────────────────────────────────────

        private bool _isDirty;
        public bool IsDirty
        {
            get => _isDirty;
            private set => this.RaiseAndSetIfChanged(ref _isDirty, value);
        }

        public SettingsViewModel()
        {
            // Mark dirty whenever any property on the editable copy changes
            EditCopy.PropertyChanged += (_, _) =>
            {
                IsDirty = true;
                // Apply theme immediately so the user sees the effect without restarting
                if (Avalonia.Application.Current != null)
                    Avalonia.Application.Current.RequestedThemeVariant = EditCopy.ThemeVariant;
            };

            var canSave = this.WhenAnyValue(x => x.IsDirty);

            SaveCommand = ReactiveCommand.Create(Save, canSave);

            OpenGitHubCommand = ReactiveCommand.Create(
                () => OpenUrl(Globals.GitHubUrl));

            OpenFeedbackCommand = ReactiveCommand.Create(
                () => OpenUrl(Globals.GitHubIssuesUrl));

            OpenInstallFolderCommand = ReactiveCommand.Create(
                () => OpenFolder(AppContext.BaseDirectory));

            OpenSettingsFolderCommand = ReactiveCommand.Create(
                () => OpenFolder(Path.GetDirectoryName(AppSettings.SettingsFilePath)!));

            ClearTokenCacheCommand = ReactiveCommand.CreateFromTask(ClearTokenCacheAsync);
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
            // Copy edited values back to the singleton and persist
            var target = AppSettings.Default;
            target.CopyFrom(EditCopy);
            target.Save();
            IsDirty = false;
        }

        // ── Static helpers ─────────────────────────────────────────────────────

        private static AppSettings CloneSettings()
        {
            // Shallow-clone the current settings so the editor works on a copy
            var src = AppSettings.Default;
            return new AppSettings
            {
                Theme = src.Theme,
                CopyToClipboardTimeToLive = src.CopyToClipboardTimeToLive,
                AboutToExpireWarningPeriod = src.AboutToExpireWarningPeriod,
                AboutToExpireItemColor = src.AboutToExpireItemColor,
                ExpiredItemColor = src.ExpiredItemColor,
                DisabledItemColor = src.DisabledItemColor,
                JsonConfigurationFilesRoot = src.JsonConfigurationFilesRoot,
                VaultsJsonFileLocation = src.VaultsJsonFileLocation,
                VaultAliasesJsonFileLocation = src.VaultAliasesJsonFileLocation,
                SecretKindsJsonFileLocation = src.SecretKindsJsonFileLocation,
                CustomTagsJsonFileLocation = src.CustomTagsJsonFileLocation,
                UserAccountNames = src.UserAccountNames,
                LastUsedVaultAlias = src.LastUsedVaultAlias,
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
