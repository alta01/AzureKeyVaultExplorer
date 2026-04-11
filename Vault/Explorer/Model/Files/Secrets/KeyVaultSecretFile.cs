// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Vault.Explorer.Model.Files.Secrets
{
    using Newtonsoft.Json;

    /// <summary>
    ///     Represents .kv-secret file
    /// </summary>
    [JsonObject]
    public class KeyVaultSecretFile : KeyVaultFile<SecretFileData>
    {
        public KeyVaultSecretFile()
        {
        }

        public KeyVaultSecretFile(SecretFileData data) : base(data)
        {
        }
    }
}
