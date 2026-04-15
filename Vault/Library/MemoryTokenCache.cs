// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Vault.Library
{
    using System;
    using System.Runtime.InteropServices;
    using System.Security.Cryptography;
    using Microsoft.Identity.Client;

    public class MemoryTokenCache
    {
        private static readonly object BufferLock = new object();
        private static byte[] _buffer;

        public MemoryTokenCache() { }

        public void Clear()
        {
            lock (BufferLock) { _buffer = null; }
        }

        public void ConfigureTokenCache(ITokenCache tokenCache)
        {
            tokenCache.SetBeforeAccess(this.BeforeAccessNotification);
            tokenCache.SetAfterAccess(this.AfterAccessNotification);
        }

        private void BeforeAccessNotification(TokenCacheNotificationArgs args)
        {
            lock (BufferLock)
            {
                if (_buffer != null)
                {
                    try
                    {
                        args.TokenCache.DeserializeMsalV3(Unprotect(_buffer));
                    }
                    catch
                    {
                        this.Clear();
                    }
                }
            }
        }

        private void AfterAccessNotification(TokenCacheNotificationArgs args)
        {
            if (args.HasStateChanged)
            {
                lock (BufferLock)
                {
                    try
                    {
                        _buffer = Protect(args.TokenCache.SerializeMsalV3());
                    }
                    catch { }
                }
            }
        }

        /// <summary>
        /// On Windows uses DPAPI (LocalMachine scope) for encryption.
        /// On macOS / Linux the bytes are stored as-is; the process address space
        /// provides adequate protection for an in-memory cache.
        /// </summary>
        private static byte[] Protect(byte[] data) =>
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? ProtectedData.Protect(data, null, DataProtectionScope.LocalMachine)
                : data;

        private static byte[] Unprotect(byte[] data) =>
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? ProtectedData.Unprotect(data, null, DataProtectionScope.LocalMachine)
                : data;
    }
}
