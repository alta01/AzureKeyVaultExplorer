// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Vault.Explorer.Model.Files.Secrets
{
    using System;
    using System.Collections.Generic;
    using Azure.Security.KeyVault.Certificates;
    using Microsoft.Vault.Library;
    using Newtonsoft.Json;

    /// <summary>
    ///     DTO used to persist a <see cref="KeyVaultCertificate"/> to a .kv-certificate file.
    ///     Replaces the old CertificateBundle dependency.
    /// </summary>
    [JsonObject]
    public class CertificateFileData
    {
        [JsonProperty]
        public string Id { get; set; }

        [JsonProperty]
        public byte[] Cer { get; set; }

        [JsonProperty]
        public bool? Enabled { get; set; }

        [JsonProperty]
        public DateTimeOffset? ExpiresOn { get; set; }

        [JsonProperty]
        public DateTimeOffset? NotBefore { get; set; }

        [JsonProperty]
        public Dictionary<string, string> Tags { get; set; }

        public static CertificateFileData FromCertificate(KeyVaultCertificate c)
        {
            var data = new CertificateFileData
            {
                Id = c.Id?.ToString(),
                Cer = c.Cer,
                Enabled = c.Properties.Enabled,
                ExpiresOn = c.Properties.ExpiresOn,
                NotBefore = c.Properties.NotBefore,
            };
            if (c.Properties.Tags.Count > 0)
            {
                data.Tags = new Dictionary<string, string>(c.Properties.Tags);
            }

            return data;
        }

        public ObjectIdentifier ToObjectIdentifier()
        {
            if (string.IsNullOrEmpty(this.Id))
                return new ObjectIdentifier(null, null, null, null);
            var uri = new Uri(this.Id);
            var segments = uri.AbsolutePath.TrimStart('/').Split('/');
            string name = segments.Length > 1 ? segments[1] : null;
            string version = segments.Length > 2 ? segments[2] : string.Empty;
            string vault = $"{uri.Scheme}://{uri.Host}";
            return new ObjectIdentifier(name, this.Id, version, vault);
        }
    }
}
