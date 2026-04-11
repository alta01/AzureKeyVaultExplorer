// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Vault.Library
{
    using System;
    using Azure.Core;
    using Azure.Security.KeyVault.Certificates;
    using Azure.Security.KeyVault.Secrets;

    /// <summary>
    ///     Holds <see cref="SecretClient"/> and <see cref="CertificateClient"/> for a single vault,
    ///     replacing the old KeyVaultClientEx which extended the deprecated KeyVaultClient.
    /// </summary>
    internal sealed class VaultKeyValueClient
    {
        public readonly string VaultName;
        public readonly string VaultUri;
        public readonly SecretClient SecretClient;
        public readonly CertificateClient CertificateClient;

        internal VaultKeyValueClient(string vaultName, TokenCredential credential)
        {
            Utils.GuardVaultName(vaultName);
            this.VaultName = vaultName;
            this.VaultUri = string.Format(Consts.AzureVaultUriFormat, vaultName);
            var vaultUri = new Uri(this.VaultUri);
            this.SecretClient = new SecretClient(vaultUri, credential);
            this.CertificateClient = new CertificateClient(vaultUri, credential);
        }

        private string ToIdentifier(string endpoint, string name, string version) =>
            $"{this.VaultUri}/{endpoint}/{name}" + (string.IsNullOrEmpty(version) ? "" : $"/{version}");

        public string ToSecretIdentifier(string secretName, string version = null) =>
            this.ToIdentifier(Consts.SecretsEndpoint, secretName, version);

        public string ToCertificateIdentifier(string certificateName, string version = null) =>
            this.ToIdentifier(Consts.CertificatesEndpoint, certificateName, version);

        public override string ToString() => this.VaultUri;
    }
}
