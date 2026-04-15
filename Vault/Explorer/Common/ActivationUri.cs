// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Vault.Explorer.Common
{
    using System;
    using System.Runtime.InteropServices;
    using System.Threading;
    using Microsoft.Vault.Library;

    public class ActivationUri : VaultLinkUri
    {
        public static readonly ActivationUri Empty = new ActivationUri("vault:");

        public ActivationUri(string vaultUri) : base(vaultUri)
        {
        }

        /// <summary>
        /// Parses the activation URI from command-line arguments.
        /// ClickOnce network-deploy is retired (Phase 5); only CLI arg form is supported.
        /// </summary>
        public new static ActivationUri Parse()
        {
            string vaultUri = Environment.GetCommandLineArgs().Length == 2
                ? Environment.GetCommandLineArgs()[1]
                : null;

            if (string.IsNullOrEmpty(vaultUri))
                return Empty;

            if (vaultUri.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
                return Empty;

            // Strip optional online activation prefix
            if (vaultUri.StartsWith(Globals.OnlineActivationUri, StringComparison.OrdinalIgnoreCase))
                vaultUri = vaultUri.Substring(Globals.OnlineActivationUri.Length).TrimStart('?');

            if (string.IsNullOrEmpty(vaultUri))
                return Empty;

            if (!vaultUri.StartsWith("vault:", StringComparison.OrdinalIgnoreCase))
                return Empty;

            return new ActivationUri(vaultUri.TrimEnd('/', '\\'));
        }

        /// <summary>
        /// Registers the vault: protocol handler for the current user.
        /// Windows: HKCU registry entry.
        /// macOS / Linux: delegated to IProtocolHandlerService (wired in App.axaml.cs).
        /// </summary>
        public static void RegisterVaultProtocol()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return; // Non-Windows handled by IProtocolHandlerService in DI

            try
            {
                using var vaultKey = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\Classes\vault");
                vaultKey.SetValue("", "URL:Vault Protocol");
                vaultKey.SetValue("URL Protocol", "");
                vaultKey.CreateSubKey("DefaultIcon").SetValue("", $"{AppContext.BaseDirectory}VaultExplorer.exe,0");
                vaultKey.CreateSubKey("shell").CreateSubKey("open").CreateSubKey("command")
                    .SetValue("", $"\"{AppContext.BaseDirectory}VaultExplorer.exe\" \"%1\"");

                // Enable trust in Office for vault: protocol
                for (int officeVersion = 14; officeVersion < 30; officeVersion++)
                {
                    string version = $"{officeVersion}.0";
                    if (null != Microsoft.Win32.Registry.CurrentUser.OpenSubKey($@"Software\Policies\Microsoft\Office\{version}"))
                    {
                        Microsoft.Win32.Registry.CurrentUser.CreateSubKey(
                            $@"Software\Policies\Microsoft\Office\{version}\common\security\trusted protocols\all applications\vault:");
                    }
                }
            }
            catch { /* Non-fatal */ }
        }
    }
}
