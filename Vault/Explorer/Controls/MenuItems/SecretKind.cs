// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Vault.Explorer.Controls.MenuItems
{
    using System;
    using System.Text.RegularExpressions;
    using Microsoft.Vault.Library;
    using Newtonsoft.Json;

    /// <summary>
    /// Describes a named secret kind (pattern, validation rules, expiration policy).
    /// Deserialized from SecretKinds.json.
    /// Previously extended ToolStripMenuItem; now a plain POCO (Phase 5).
    /// </summary>
    [JsonObject]
    public class SecretKind
    {
        [JsonProperty]
        public readonly string Alias;

        [JsonProperty]
        public readonly string Description;

        [JsonProperty]
        public readonly Regex NameRegex;

        [JsonProperty]
        public readonly Regex ValueRegex;

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public readonly string ValueTemplate;

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public readonly string CertificateFormat;

        [JsonIgnore]
        public bool IsCertificate => !string.IsNullOrEmpty(this.CertificateFormat);

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public readonly string[] RequiredCustomTags;

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public readonly string[] OptionalCustomTags;

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public readonly TimeSpan DefaultExpiration;

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public readonly TimeSpan MaxExpiration;

        public SecretKind()
        {
            this.Alias = "Custom";
            this.Description = "The name must be a string 1-127 characters in length containing only 0-9, a-z, A-Z, and -.";
            this.NameRegex = Consts.ValidSecretNameRegex;
            this.ValueRegex = new Regex("^.{0,1048576}$", RegexOptions.Singleline | RegexOptions.Compiled);
            this.ValueTemplate = "";
            this.CertificateFormat = null;
            this.RequiredCustomTags = new string[0];
            this.OptionalCustomTags = new string[0];
            this.MaxExpiration = TimeSpan.MaxValue;
        }

        public SecretKind(string alias) : this()
        {
            this.Alias = alias;
        }

        [JsonConstructor]
        public SecretKind(string alias, string description, string nameRegex, string valueRegex,
            string valueTemplate, string certificateFormat,
            string[] requiredCustomTags, string[] optionalCustomTags,
            TimeSpan defaultExpiration, TimeSpan maxExpiration)
        {
            this.Alias = alias;
            this.Description = description;
            this.NameRegex = new Regex(nameRegex, RegexOptions.Singleline | RegexOptions.Compiled);
            this.ValueRegex = new Regex(valueRegex, RegexOptions.Singleline | RegexOptions.Compiled);
            this.ValueTemplate = valueTemplate;
            this.CertificateFormat = certificateFormat;
            this.RequiredCustomTags = requiredCustomTags ?? new string[0];
            this.OptionalCustomTags = optionalCustomTags ?? new string[0];
            this.DefaultExpiration = defaultExpiration;
            this.MaxExpiration = default(TimeSpan) == maxExpiration ? TimeSpan.MaxValue : maxExpiration;

            if (this.DefaultExpiration > this.MaxExpiration)
                throw new ArgumentOutOfRangeException(
                    "DefaultExpiration or MaxExpiration",
                    $"DefaultExpiration must be less than MaxExpiration in secret kind {alias}");

            if (this.RequiredCustomTags.Length + this.OptionalCustomTags.Length > Consts.MaxNumberOfTags)
                throw new ArgumentOutOfRangeException(
                    "Total CustomTags.Length",
                    $"Too many custom tags for secret kind {alias}, maximum is {Consts.MaxNumberOfTags}");
        }

        public override string ToString() => this.Alias;
    }
}
