// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Vault.Library
{
    /// <summary>
    /// Local replacement for the deprecated Microsoft.Azure.KeyVault.ObjectIdentifier.
    /// Provides the same Name/Identifier/Version/Vault surface used throughout the UI layer.
    /// </summary>
    public sealed class ObjectIdentifier
    {
        /// <summary>The object name (e.g. "my-secret").</summary>
        public string Name { get; }

        /// <summary>The full versioned URI (e.g. "https://vault.azure.net/secrets/name/version").</summary>
        public string Identifier { get; }

        /// <summary>The version string, or empty string for the latest version.</summary>
        public string Version { get; }

        /// <summary>The base vault URI (e.g. "https://vault.azure.net").</summary>
        public string Vault { get; }

        public ObjectIdentifier(string name, string identifier, string version, string vault)
        {
            this.Name = name ?? string.Empty;
            this.Identifier = identifier ?? string.Empty;
            this.Version = version ?? string.Empty;
            this.Vault = vault ?? string.Empty;
        }
    }
}
