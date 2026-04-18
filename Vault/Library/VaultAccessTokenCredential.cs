// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Vault.Library
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Core;
    using Azure.Identity;
    using Microsoft.Identity.Client;

    /// <summary>
    /// Bridges the existing <see cref="VaultAccess"/> MSAL-based auth to the
    /// <see cref="TokenCredential"/> contract required by Azure SDK Track-2 clients.
    /// Tries each <see cref="VaultAccess"/> in order, returning the first successful token.
    /// </summary>
    internal sealed class VaultAccessTokenCredential : TokenCredential
    {
        private readonly VaultAccess[] _vaultAccesses;
        private readonly string _userAliasType;
        private readonly string _vaultName;
        private readonly Action<string> _onAuthenticated;

        internal VaultAccessTokenCredential(VaultAccess[] vaultAccesses, string userAliasType, string vaultName, Action<string> onAuthenticated)
        {
            _vaultAccesses = vaultAccesses;
            _userAliasType = userAliasType ?? string.Empty;
            _vaultName = vaultName;
            _onAuthenticated = onAuthenticated;
        }

        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
            => GetTokenAsync(requestContext, cancellationToken).GetAwaiter().GetResult();

        public override async ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            // Try a targeted credential chain first: AzureCliCredential picks up `az login`
            // immediately without browser prompts; ManagedIdentityCredential covers Azure-hosted.
            // Critically, OperationCanceledException is re-thrown so the Cancel button works.
            try
            {
                var credential = new ChainedTokenCredential(
                    new AzureCliCredential(),
                    new ManagedIdentityCredential()
                );
                var token = await credential.GetTokenAsync(requestContext, cancellationToken).ConfigureAwait(false);
                _onAuthenticated?.Invoke(_userAliasType);
                return token;
            }
            catch (OperationCanceledException) { throw; }              // propagate Cancel
            catch (CredentialUnavailableException) { /* fall through */ }
            catch { /* fall through to MSAL-based VaultAccess chain */ }

            Queue<Exception> exceptions = new Queue<Exception>();
            string vaultAccessTypes = "";
            foreach (VaultAccess va in _vaultAccesses)
            {
                try
                {
                    AuthenticationResult result = await va.AcquireTokenAsync(requestContext.Scopes, _userAliasType).ConfigureAwait(false);
                    if (result.Account == null)
                        throw new VaultAccessException("The authentication result doesn't include account information");
                    _onAuthenticated?.Invoke(result.Account.Username ?? _userAliasType);
                    return new AccessToken(result.AccessToken, result.ExpiresOn);
                }
                catch (Exception e)
                {
                    vaultAccessTypes += $" {va}";
                    exceptions.Enqueue(e);
                }
            }
            throw new VaultAccessException($"Failed to get access to {_vaultName} with all possible vault access type(s){vaultAccessTypes}", exceptions.ToArray());
        }
    }
}
