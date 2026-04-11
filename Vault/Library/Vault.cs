// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Vault.Library
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure;
    using Azure.Security.KeyVault.Certificates;
    using Azure.Security.KeyVault.Secrets;
    using Microsoft.Vault.Core;
    using Newtonsoft.Json;

    /// <summary>
    ///     Single or dual Vault class to manage secrets
    ///     For HA and DR story this supports up to two Azure Key Vaults, one in each region in the specified geo
    /// </summary>
    /// <remarks>
    ///     Based on vault name and <see cref="Vault.VaultsConfig" /> appropriate access will be picked in the following order:
    ///     1. <see cref="VaultAccessClientCertificate" /> - client id (application id) in AzureAD will be selected with right
    ///     certificate thumbprint (sha1) of the application's principal to get the access
    ///     2. <see cref="VaultAccessClientCredential" /> - client id and client secret will be used to get the access
    ///     3. <see cref="VaultAccessUserInteractive" /> - client id (powershell app id) and user credentials will be used to
    ///     get the access
    /// </remarks>
    public class Vault
    {
        private readonly VaultKeyValueClient[] _keyVaultClients;
        private bool Secondary => this._keyVaultClients.Length == 2;

        public readonly string VaultsConfigFile;
        public readonly string[] VaultNames;
        public readonly VaultsConfig VaultsConfig;

        private static readonly Task CompletedTask = Task.FromResult(0);
        private static readonly object Lock = new object();

        /// <summary>
        ///     UserPrincipalName, in UPN format of the currently authenticated user, in case of cert based access the value will
        ///     be: {Environment.UserDomainName}\{Environment.UserName}
        ///     The value will be set only after successful operation to vault.
        /// </summary>
        public string AuthenticatedUserName { get; private set; }

        /// <summary>
        ///     Delegate to indicate progress
        /// </summary>
        /// <param name="position">Current position in the list of secrets, keys or certificates</param>
        public delegate void ListOperationProgressUpdate(int position);

        #region Constructors

        /// <summary>
        ///     Creates the vault management instance based on provided Vaults Config dictionary
        /// </summary>
        public Vault(VaultsConfig vaultsConfig, VaultAccessTypeEnum accessType, params string[] vaultNames)
        {
            Guard.ArgumentNotNull(vaultsConfig, nameof(vaultsConfig));
            Guard.ArgumentCollectionNotEmpty(vaultNames, nameof(vaultNames));
            this.VaultsConfig = vaultsConfig;
            this.VaultNames = vaultNames.Where(Extensions.IsNotEmpty).ToArray();
            switch (this.VaultNames.Length)
            {
                case 1:
                    this._keyVaultClients = new[]
                    {
                        this.CreateVaultKeyValueClient(accessType, this.VaultNames[0]),
                    };
                    break;
                case 2:
                    string primaryVaultName = this.VaultNames[0];
                    string secondaryVaultName = this.VaultNames[1];
                    if (0 == string.Compare(primaryVaultName, secondaryVaultName, true))
                    {
                        throw new ArgumentException($"Primary vault name {primaryVaultName} is equal to secondary vault name {secondaryVaultName}");
                    }

                    this._keyVaultClients = new VaultKeyValueClient[2]
                    {
                        this.CreateVaultKeyValueClient(accessType, primaryVaultName),
                        this.CreateVaultKeyValueClient(accessType, secondaryVaultName),
                    };
                    break;
                default:
                    throw new ArgumentException("Vault names length must be 1 or 2 only", nameof(this.VaultNames));
            }
        }

        /// <summary>
        ///     Load specified Vaults.json configuration file and creates the vault management instance
        /// </summary>
        public Vault(string vaultsConfigFile, VaultAccessTypeEnum accessType, params string[] vaultNames)
            : this(DeserializeVaultsConfigFromFile(ref vaultsConfigFile), accessType, vaultNames)
        {
            this.VaultsConfigFile = vaultsConfigFile;
        }

        /// <summary>
        ///     Single (primary) vault management constructor
        /// </summary>
        public Vault(VaultAccessTypeEnum accessType, params string[] vaultNames)
            : this(string.Empty, accessType, vaultNames)
        {
        }

        /// <summary>
        ///     Single (primary) or Dual (primary and secondary) vault management constructor
        /// </summary>
        public Vault(VaultAccessTypeEnum accessType, string vaultName)
            : this(accessType, new[] { vaultName })
        {
        }

        /// <summary>
        ///     Dual (primary and secondary) vault management constructor
        /// </summary>
        public Vault(VaultAccessTypeEnum accessType, string primaryVaultName, string secondaryVaultName)
            : this(accessType, new[] { primaryVaultName, secondaryVaultName })
        {
        }

        private static VaultsConfig DeserializeVaultsConfigFromFile(ref string vaultsConfigFile)
        {
            var settings = new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.Auto,
            };
            if (string.IsNullOrWhiteSpace(vaultsConfigFile))
            {
                vaultsConfigFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Consts.VaultsJsonConfig);
                if (!File.Exists(vaultsConfigFile))
                {
                    vaultsConfigFile = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), Consts.VaultsJsonConfig);
                }
            }

            return JsonConvert.DeserializeObject<VaultsConfig>(File.ReadAllText(vaultsConfigFile), settings);
        }

        private VaultKeyValueClient CreateVaultKeyValueClient(VaultAccessTypeEnum accessType, string vaultName)
        {
            VaultAccess[] vas;
            string userAliasType;

            lock (Lock)
            {
                Utils.GuardVaultName(vaultName);
                if (false == this.VaultsConfig.ContainsKey(vaultName))
                {
                    throw new KeyNotFoundException($"{vaultName} is not found in {this.VaultsConfigFile}");
                }

                VaultAccessType vat = this.VaultsConfig[vaultName];
                vas = accessType == VaultAccessTypeEnum.ReadOnly ? vat.ReadOnly : vat.ReadWrite;
                vas = vas.OrderBy(va => va.Order).ToArray();
                userAliasType = (from va in vas where va is VaultAccessUserInteractive select (VaultAccessUserInteractive)va).FirstOrDefault()?.UserAliasType;
            }

            var credential = new VaultAccessTokenCredential(vas, userAliasType, vaultName, username =>
            {
                this.AuthenticatedUserName = username;
            });

            return new VaultKeyValueClient(vaultName, credential);
        }

        #endregion

        #region Secrets

        /// <summary>
        ///     Gets specified secret by name from vault.
        ///     Prefers vault in same region; falls back to other region on failure.
        /// </summary>
        public async Task<KeyVaultSecret> GetSecretAsync(string secretName, string secretVersion = null, CancellationToken cancellationToken = default)
        {
            Queue<Exception> exceptions = new Queue<Exception>();
            string vaults = "";
            foreach (var kv in this._keyVaultClients)
            {
                try
                {
                    var response = await kv.SecretClient.GetSecretAsync(
                        secretName,
                        string.IsNullOrEmpty(secretVersion) ? null : secretVersion,
                        cancellationToken).ConfigureAwait(false);
                    return response.Value;
                }
                catch (Exception e)
                {
                    vaults += $" {kv}";
                    exceptions.Enqueue(e);
                }
            }

            throw new SecretException($"Failed to get secret {secretName} from vault(s){vaults}", exceptions.ToArray());
        }

        /// <summary>
        ///     Sets a secret in both vaults
        /// </summary>
        public async Task<KeyVaultSecret> SetSecretAsync(string secretName, string value, Dictionary<string, string> tags = null, string contentType = null, bool? enabled = null, DateTimeOffset? expires = null, DateTimeOffset? notBefore = null, CancellationToken cancellationToken = default)
        {
            tags = Utils.AddMd5ChangedBy(tags, value, this.AuthenticatedUserName);
            var secret = new KeyVaultSecret(secretName, value);
            secret.Properties.ContentType = contentType;
            secret.Properties.Enabled = enabled;
            secret.Properties.ExpiresOn = expires;
            secret.Properties.NotBefore = notBefore;
            if (tags != null)
            {
                foreach (var kvp in tags) secret.Properties.Tags[kvp.Key] = kvp.Value;
            }

            var t0 = this._keyVaultClients[0].SecretClient.SetSecretAsync(secret, cancellationToken);
            var t1 = this.Secondary ? this._keyVaultClients[1].SecretClient.SetSecretAsync(secret, cancellationToken) : Task.FromResult<Response<KeyVaultSecret>>(null);
            await Task.WhenAll(t0, t1).ContinueWith(t =>
            {
                if (t0.IsFaulted && t1.IsFaulted)
                {
                    throw new SecretException($"Failed to set secret {secretName} in both vaults {this._keyVaultClients[0]} and {this._keyVaultClients[1]}", t0.Exception, t1.Exception);
                }

                if (t0.IsFaulted)
                {
                    throw new SecretException($"Failed to set secret {secretName} in vault {this._keyVaultClients[0]}", t0.Exception);
                }

                if (t1.IsFaulted)
                {
                    throw new SecretException($"Failed to set secret {secretName} in vault {this._keyVaultClients[1]}", t1.Exception);
                }
            });
            return t0.Result.Value;
        }

        /// <summary>
        ///     Updates the attributes associated with the specified secret in both vaults.
        ///     Fetches the current version first so UpdateSecretPropertiesAsync gets a versioned URI.
        /// </summary>
        public async Task<SecretProperties> UpdateSecretAsync(string secretName, string secretVersion = null, Dictionary<string, string> tags = null, string contentType = null, bool? enabled = null, DateTimeOffset? expires = null, DateTimeOffset? notBefore = null, CancellationToken cancellationToken = default)
        {
            tags = Utils.AddMd5ChangedBy(tags, null, this.AuthenticatedUserName);
            string version = string.IsNullOrEmpty(secretVersion) ? null : secretVersion;

            var current0 = await this._keyVaultClients[0].SecretClient.GetSecretAsync(secretName, version, cancellationToken).ConfigureAwait(false);
            var props0 = current0.Value.Properties;
            ApplyToSecretProperties(props0, tags, contentType, enabled, expires, notBefore);
            var t0 = this._keyVaultClients[0].SecretClient.UpdateSecretPropertiesAsync(props0, cancellationToken);

            Task<Response<SecretProperties>> t1 = Task.FromResult<Response<SecretProperties>>(null);
            if (this.Secondary)
            {
                var current1 = await this._keyVaultClients[1].SecretClient.GetSecretAsync(secretName, version, cancellationToken).ConfigureAwait(false);
                var props1 = current1.Value.Properties;
                ApplyToSecretProperties(props1, tags, contentType, enabled, expires, notBefore);
                t1 = this._keyVaultClients[1].SecretClient.UpdateSecretPropertiesAsync(props1, cancellationToken);
            }

            await Task.WhenAll(t0, t1).ContinueWith(t =>
            {
                if (t0.IsFaulted && t1.IsFaulted)
                {
                    throw new SecretException($"Failed to update secret {secretName} in both vaults {this._keyVaultClients[0]} and {this._keyVaultClients[1]}", t0.Exception, t1.Exception);
                }

                if (t0.IsFaulted)
                {
                    throw new SecretException($"Failed to update secret {secretName} in vault {this._keyVaultClients[0]}", t0.Exception);
                }

                if (t1.IsFaulted)
                {
                    throw new SecretException($"Failed to update secret {secretName} in vault {this._keyVaultClients[1]}", t1.Exception);
                }
            });
            return t0.Result.Value;
        }

        private static void ApplyToSecretProperties(SecretProperties props, Dictionary<string, string> tags, string contentType, bool? enabled, DateTimeOffset? expires, DateTimeOffset? notBefore)
        {
            props.ContentType = contentType;
            props.Enabled = enabled;
            props.ExpiresOn = expires;
            props.NotBefore = notBefore;
            props.Tags.Clear();
            if (tags != null)
            {
                foreach (var kvp in tags) props.Tags[kvp.Key] = kvp.Value;
            }
        }

        /// <summary>
        ///     List all secrets from specified vault
        /// </summary>
        public async Task<IEnumerable<SecretProperties>> ListSecretsAsync(int regionIndex = 0, ListOperationProgressUpdate listSecretsProgressUpdate = null, CancellationToken cancellationToken = default)
        {
            Guard.ArgumentIsValidRegion(regionIndex, nameof(regionIndex));
            Guard.ArgumentInRange(regionIndex, 0, this._keyVaultClients.Length - 1, nameof(regionIndex));
            var result = new Dictionary<string, SecretProperties>(StringComparer.InvariantCulture);
            await foreach (var sp in this._keyVaultClients[regionIndex].SecretClient.GetPropertiesOfSecretsAsync(cancellationToken).ConfigureAwait(false))
            {
                result[sp.Name] = sp;
                listSecretsProgressUpdate?.Invoke(result.Count);
            }

            return result.Values;
        }

        /// <summary>
        ///     List all the versions of a specified secret
        /// </summary>
        public async Task<IEnumerable<SecretProperties>> GetSecretVersionsAsync(string secretName, int regionIndex = 0, CancellationToken cancellationToken = default)
        {
            Guard.ArgumentNotNullOrWhitespace(secretName, nameof(secretName));
            Guard.ArgumentIsValidRegion(regionIndex, nameof(regionIndex));
            Guard.ArgumentInRange(regionIndex, 0, this._keyVaultClients.Length - 1, nameof(regionIndex));

            var result = new Dictionary<string, SecretProperties>(StringComparer.InvariantCulture);
            await foreach (var sp in this._keyVaultClients[regionIndex].SecretClient.GetPropertiesOfSecretVersionsAsync(secretName, cancellationToken).ConfigureAwait(false))
            {
                result[sp.Id.ToString()] = sp;
            }

            return result.Values;
        }

        /// <summary>
        ///     Deletes a secret from both vaults
        /// </summary>
        public async Task DeleteSecretAsync(string secretName, CancellationToken cancellationToken = default)
        {
            var t0 = this._keyVaultClients[0].SecretClient.StartDeleteSecretAsync(secretName, cancellationToken);
            var t1 = this.Secondary ? this._keyVaultClients[1].SecretClient.StartDeleteSecretAsync(secretName, cancellationToken) : Task.FromResult<DeleteSecretOperation>(null);
            await Task.WhenAll(t0, t1).ContinueWith(t =>
            {
                if (t0.IsFaulted && t1.IsFaulted)
                {
                    throw new SecretException($"Failed to delete secret {secretName} from both vaults {this._keyVaultClients[0]} and {this._keyVaultClients[1]}", t0.Exception, t1.Exception);
                }

                if (t0.IsFaulted)
                {
                    throw new SecretException($"Failed to delete secret {secretName} from vault {this._keyVaultClients[0]}", t0.Exception);
                }

                if (t1.IsFaulted)
                {
                    throw new SecretException($"Failed to delete secret {secretName} from vault {this._keyVaultClients[1]}", t1.Exception);
                }
            });
        }

        #endregion

        #region Certificates

        /// <summary>
        ///     Gets a certificate with exportable private key by fetching its secret representation.
        /// </summary>
        public async Task<X509Certificate2> GetCertificateWithExportableKeysAsync(string certificateName, string certificateVersion = null, CancellationToken cancellationToken = default)
        {
            KeyVaultSecret s = await this.GetSecretAsync(certificateName, certificateVersion, cancellationToken);
            return X509CertificateLoader.LoadPkcs12(Convert.FromBase64String(s.Value), string.Empty, X509KeyStorageFlags.Exportable, Pkcs12LoaderLimits.Defaults);
        }

        /// <summary>
        ///     Gets a certificate. Falls back to secondary on failure.
        /// </summary>
        public async Task<KeyVaultCertificate> GetCertificateAsync(string certificateName, string certificateVersion = null, CancellationToken cancellationToken = default)
        {
            Queue<Exception> exceptions = new Queue<Exception>();
            string vaults = "";
            foreach (var kv in this._keyVaultClients)
            {
                try
                {
                    Response<KeyVaultCertificate> response;
                    if (string.IsNullOrEmpty(certificateVersion))
                        response = await kv.CertificateClient.GetCertificateAsync(certificateName, cancellationToken).ConfigureAwait(false);
                    else
                        response = await kv.CertificateClient.GetCertificateVersionAsync(certificateName, certificateVersion, cancellationToken).ConfigureAwait(false);
                    return response.Value;
                }
                catch (Exception e)
                {
                    vaults += $" {kv}";
                    exceptions.Enqueue(e);
                }
            }

            throw new SecretException($"Failed to get certificate {certificateName} from vault(s){vaults}", exceptions.ToArray());
        }

        /// <summary>
        ///     Gets the management policy for a certificate from the primary vault.
        /// </summary>
        public async Task<CertificatePolicy> GetCertificatePolicyAsync(string certificateName, CancellationToken cancellationToken = default)
        {
            var response = await this._keyVaultClients[0].CertificateClient.GetCertificatePolicyAsync(certificateName, cancellationToken).ConfigureAwait(false);
            return response.Value;
        }

        /// <summary>
        ///     List all certificates from specified vault
        /// </summary>
        public async Task<IEnumerable<CertificateProperties>> ListCertificatesAsync(int regionIndex = 0, ListOperationProgressUpdate listCertificatesProgressUpdate = null, CancellationToken cancellationToken = default)
        {
            Guard.ArgumentIsValidRegion(regionIndex, nameof(regionIndex));
            Guard.ArgumentInRange(regionIndex, 0, this._keyVaultClients.Length - 1, nameof(regionIndex));
            var result = new Dictionary<string, CertificateProperties>(StringComparer.InvariantCulture);
            await foreach (var cp in this._keyVaultClients[regionIndex].CertificateClient.GetPropertiesOfCertificatesAsync(cancellationToken: cancellationToken).ConfigureAwait(false))
            {
                result[cp.Name] = cp;
                listCertificatesProgressUpdate?.Invoke(result.Count);
            }

            return result.Values;
        }

        /// <summary>
        ///     Imports a new certificate version into both vaults.
        /// </summary>
        public async Task<KeyVaultCertificate> ImportCertificateAsync(string certificateName, X509Certificate2Collection certificateCollection, CertificatePolicy certificatePolicy, bool? enabled = null, IDictionary<string, string> tags = null, CancellationToken cancellationToken = default)
        {
            string thumbprint = certificateCollection.FirstOrDefault()?.Thumbprint.ToLowerInvariant();
            tags = Utils.AddMd5ChangedBy(tags, thumbprint, this.AuthenticatedUserName);

            byte[] pfxBytes = certificateCollection.Export(X509ContentType.Pkcs12);
            var options = new ImportCertificateOptions(certificateName, pfxBytes)
            {
                Policy = certificatePolicy,
                Enabled = enabled,
            };
            if (tags != null)
            {
                foreach (var kvp in tags) options.Tags[kvp.Key] = kvp.Value;
            }

            var t0 = this._keyVaultClients[0].CertificateClient.ImportCertificateAsync(options, cancellationToken);
            var t1 = this.Secondary ? this._keyVaultClients[1].CertificateClient.ImportCertificateAsync(options, cancellationToken) : Task.FromResult<Response<KeyVaultCertificate>>(null);
            await Task.WhenAll(t0, t1).ContinueWith(t =>
            {
                if (t0.IsFaulted && t1.IsFaulted)
                {
                    throw new SecretException($"Failed to import certificate {certificateName} to both vaults {this._keyVaultClients[0]} and {this._keyVaultClients[1]}", t0.Exception, t1.Exception);
                }

                if (t0.IsFaulted)
                {
                    throw new SecretException($"Failed to import certificate {certificateName} to vault {this._keyVaultClients[0]}", t0.Exception);
                }

                if (t1.IsFaulted)
                {
                    throw new SecretException($"Failed to import certificate {certificateName} to vault {this._keyVaultClients[1]}", t1.Exception);
                }
            });
            return t0.Result.Value;
        }

        /// <summary>
        ///     Deletes a certificate from both vaults.
        /// </summary>
        public async Task DeleteCertificateAsync(string certificateName, CancellationToken cancellationToken = default)
        {
            var t0 = this._keyVaultClients[0].CertificateClient.StartDeleteCertificateAsync(certificateName, cancellationToken);
            var t1 = this.Secondary ? this._keyVaultClients[1].CertificateClient.StartDeleteCertificateAsync(certificateName, cancellationToken) : Task.FromResult<DeleteCertificateOperation>(null);
            await Task.WhenAll(t0, t1).ContinueWith(t =>
            {
                if (t0.IsFaulted && t1.IsFaulted)
                {
                    throw new SecretException($"Failed to delete certificate {certificateName} from both vaults {this._keyVaultClients[0]} and {this._keyVaultClients[1]}", t0.Exception, t1.Exception);
                }

                if (t0.IsFaulted)
                {
                    throw new SecretException($"Failed to delete certificate {certificateName} from vault {this._keyVaultClients[0]}", t0.Exception);
                }

                if (t1.IsFaulted)
                {
                    throw new SecretException($"Failed to delete certificate {certificateName} from vault {this._keyVaultClients[1]}", t1.Exception);
                }
            });
        }

        /// <summary>
        ///     Updates certificate properties in both vaults.
        ///     Fetches the current version first so UpdateCertificatePropertiesAsync gets a versioned URI.
        /// </summary>
        public async Task<KeyVaultCertificate> UpdateCertificateAsync(string certificateName, string certificateVersion = null, bool? enabled = null, DateTimeOffset? expires = null, DateTimeOffset? notBefore = null, IDictionary<string, string> tags = null, CancellationToken cancellationToken = default)
        {
            tags = Utils.AddMd5ChangedBy(tags, null, this.AuthenticatedUserName);
            string version = string.IsNullOrEmpty(certificateVersion) ? null : certificateVersion;

            Response<KeyVaultCertificate> curr0;
            if (string.IsNullOrEmpty(version))
                curr0 = await this._keyVaultClients[0].CertificateClient.GetCertificateAsync(certificateName, cancellationToken).ConfigureAwait(false);
            else
                curr0 = await this._keyVaultClients[0].CertificateClient.GetCertificateVersionAsync(certificateName, version, cancellationToken).ConfigureAwait(false);

            var props0 = curr0.Value.Properties;
            ApplyToCertificateProperties(props0, tags, enabled, expires, notBefore);
            var t0 = this._keyVaultClients[0].CertificateClient.UpdateCertificatePropertiesAsync(props0, cancellationToken);

            Task<Response<KeyVaultCertificate>> t1 = Task.FromResult<Response<KeyVaultCertificate>>(null);
            if (this.Secondary)
            {
                Response<KeyVaultCertificate> curr1;
                if (string.IsNullOrEmpty(version))
                    curr1 = await this._keyVaultClients[1].CertificateClient.GetCertificateAsync(certificateName, cancellationToken).ConfigureAwait(false);
                else
                    curr1 = await this._keyVaultClients[1].CertificateClient.GetCertificateVersionAsync(certificateName, version, cancellationToken).ConfigureAwait(false);

                var props1 = curr1.Value.Properties;
                ApplyToCertificateProperties(props1, tags, enabled, expires, notBefore);
                t1 = this._keyVaultClients[1].CertificateClient.UpdateCertificatePropertiesAsync(props1, cancellationToken);
            }

            await Task.WhenAll(t0, t1).ContinueWith(t =>
            {
                if (t0.IsFaulted && t1.IsFaulted)
                {
                    throw new SecretException($"Failed to update certificate {certificateName} in both vaults {this._keyVaultClients[0]} and {this._keyVaultClients[1]}", t0.Exception, t1.Exception);
                }

                if (t0.IsFaulted)
                {
                    throw new SecretException($"Failed to update certificate {certificateName} in vault {this._keyVaultClients[0]}", t0.Exception);
                }

                if (t1.IsFaulted)
                {
                    throw new SecretException($"Failed to update certificate {certificateName} in vault {this._keyVaultClients[1]}", t1.Exception);
                }
            });
            return t0.Result.Value;
        }

        private static void ApplyToCertificateProperties(CertificateProperties props, IDictionary<string, string> tags, bool? enabled, DateTimeOffset? expires, DateTimeOffset? notBefore)
        {
            props.Enabled = enabled;
            props.ExpiresOn = expires;
            props.NotBefore = notBefore;
            props.Tags.Clear();
            if (tags != null)
            {
                foreach (var kvp in tags) props.Tags[kvp.Key] = kvp.Value;
            }
        }

        /// <summary>
        ///     Updates the policy for a certificate in both vaults.
        /// </summary>
        public async Task<CertificatePolicy> UpdateCertificatePolicyAsync(string certificateName, CertificatePolicy certificatePolicy, CancellationToken cancellationToken = default)
        {
            var t0 = this._keyVaultClients[0].CertificateClient.UpdateCertificatePolicyAsync(certificateName, certificatePolicy, cancellationToken);
            var t1 = this.Secondary ? this._keyVaultClients[1].CertificateClient.UpdateCertificatePolicyAsync(certificateName, certificatePolicy, cancellationToken) : Task.FromResult<Response<CertificatePolicy>>(null);
            await Task.WhenAll(t0, t1).ContinueWith(t =>
            {
                if (t0.IsFaulted && t1.IsFaulted)
                {
                    throw new SecretException($"Failed to update certificate policy for {certificateName} in both vaults {this._keyVaultClients[0]} and {this._keyVaultClients[1]}", t0.Exception, t1.Exception);
                }

                if (t0.IsFaulted)
                {
                    throw new SecretException($"Failed to update certificate policy for {certificateName} in vault {this._keyVaultClients[0]}", t0.Exception);
                }

                if (t1.IsFaulted)
                {
                    throw new SecretException($"Failed to update certificate policy for {certificateName} in vault {this._keyVaultClients[1]}", t1.Exception);
                }
            });
            return t0.Result.Value;
        }

        /// <summary>
        ///     List the versions of a certificate.
        /// </summary>
        public async Task<IEnumerable<CertificateProperties>> GetCertificateVersionsAsync(string certificateName, int regionIndex = 0, CancellationToken cancellationToken = default)
        {
            Guard.ArgumentNotNullOrWhitespace(certificateName, nameof(certificateName));
            Guard.ArgumentIsValidRegion(regionIndex, nameof(regionIndex));
            Guard.ArgumentInRange(regionIndex, 0, this._keyVaultClients.Length - 1, nameof(regionIndex));

            var result = new Dictionary<string, CertificateProperties>(StringComparer.InvariantCulture);
            await foreach (var cp in this._keyVaultClients[regionIndex].CertificateClient.GetPropertiesOfCertificateVersionsAsync(certificateName, cancellationToken).ConfigureAwait(false))
            {
                result[cp.Id.ToString()] = cp;
            }

            return result.Values;
        }

        #endregion
    }
}
