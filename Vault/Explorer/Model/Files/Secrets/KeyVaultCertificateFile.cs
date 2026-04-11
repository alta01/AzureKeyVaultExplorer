// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Vault.Explorer.Model.Files.Secrets
{
    using Newtonsoft.Json;

    /// <summary>
    ///     Represents .kv-certificate file
    /// </summary>
    [JsonObject]
    public class KeyVaultCertificateFile : KeyVaultFile<CertificateFileData>
    {
        public KeyVaultCertificateFile()
        {
        }

        public KeyVaultCertificateFile(CertificateFileData data) : base(data)
        {
        }
    }
}
