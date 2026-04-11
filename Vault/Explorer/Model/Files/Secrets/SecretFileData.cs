// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Vault.Explorer.Model.Files.Secrets
{
    using System;
    using System.Collections.Generic;
    using Azure.Security.KeyVault.Secrets;
    using Microsoft.Vault.Library;
    using Newtonsoft.Json;

    /// <summary>
    ///     DTO used to persist a <see cref="KeyVaultSecret"/> to a .kv-secret file.
    ///     Replaces the old SecretBundle dependency.
    /// </summary>
    [JsonObject]
    public class SecretFileData
    {
        [JsonProperty]
        public string Id { get; set; }

        [JsonProperty]
        public string Value { get; set; }

        [JsonProperty]
        public string ContentType { get; set; }

        [JsonProperty]
        public bool? Enabled { get; set; }

        [JsonProperty]
        public DateTimeOffset? ExpiresOn { get; set; }

        [JsonProperty]
        public DateTimeOffset? NotBefore { get; set; }

        [JsonProperty]
        public Dictionary<string, string> Tags { get; set; }

        public static SecretFileData FromSecret(KeyVaultSecret s)
        {
            var data = new SecretFileData
            {
                Id = s.Id?.ToString(),
                Value = s.Value,
                ContentType = s.Properties.ContentType,
                Enabled = s.Properties.Enabled,
                ExpiresOn = s.Properties.ExpiresOn,
                NotBefore = s.Properties.NotBefore,
            };
            if (s.Properties.Tags.Count > 0)
            {
                data.Tags = new Dictionary<string, string>(s.Properties.Tags);
            }

            return data;
        }

        public static SecretFileData ForNew(string contentType) => new SecretFileData
        {
            ContentType = contentType,
        };

        public ObjectIdentifier ToObjectIdentifier()
        {
            if (string.IsNullOrEmpty(this.Id))
                return new ObjectIdentifier(null, null, null, null);
            var uri = new Uri(this.Id);
            var segments = uri.AbsolutePath.TrimStart('/').Split('/');
            // segments: ["secrets", "name", "version"] or ["secrets", "name"]
            string name = segments.Length > 1 ? segments[1] : null;
            string version = segments.Length > 2 ? segments[2] : string.Empty;
            string vault = $"{uri.Scheme}://{uri.Host}";
            return new ObjectIdentifier(name, this.Id, version, vault);
        }
    }
}
