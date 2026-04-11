// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Vault.Explorer.Dialogs.Subscriptions
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Core;

    /// <summary>
    /// A <see cref="TokenCredential"/> that wraps a pre-acquired access token for use
    /// with ARM clients in the subscriptions manager dialog.
    /// </summary>
    internal sealed class StaticTokenCredential : TokenCredential
    {
        private readonly AccessToken _token;

        internal StaticTokenCredential(string accessToken, DateTimeOffset expiresOn)
        {
            _token = new AccessToken(accessToken, expiresOn);
        }

        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
            => _token;

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
            => new ValueTask<AccessToken>(_token);
    }
}
