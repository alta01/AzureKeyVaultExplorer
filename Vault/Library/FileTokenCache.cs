// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Vault.Library
{
    using System;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Security.Cryptography;
    using Microsoft.Identity.Client;

    public class FileTokenCache
    {
        public string FileName;
        private static readonly object FileLock = new object();

        public FileTokenCache() : this("microsoft.com") { }

        public FileTokenCache(string domainHint)
        {
            this.FileName = string.Format(Consts.VaultTokenCacheFileName, domainHint);
            Directory.CreateDirectory(Path.GetDirectoryName(this.FileName));

            // Restrict file permissions on non-Windows (chmod 600).
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && File.Exists(this.FileName))
            {
                try { File.SetUnixFileMode(this.FileName, UnixFileMode.UserRead | UnixFileMode.UserWrite); }
                catch { }
            }
        }

        public static string[] GetAllFileTokenCacheLoginNames()
        {
            string dir = Consts.VaultTokenCacheDirectory;
            if (!Directory.Exists(dir))
                return Array.Empty<string>();

            string[] paths = Directory.GetFiles(dir);

            for (int i = 0; i < paths.Length; i++)
            {
                var filename = Path.GetFileName(paths[i]);
                paths[i] = filename.Split('_').Length > 1 ? filename.Split('_')[1] : filename;
            }

            return paths;
        }

        public static void ClearAllFileTokenCaches()
        {
            foreach (string token in GetAllFileTokenCacheLoginNames())
                new FileTokenCache(token).Clear();
        }

        public void Rename(string newName)
        {
            newName = string.Format(Consts.VaultTokenCacheFileName, newName);
            if (File.Exists(newName)) File.Delete(newName);
            File.Move(this.FileName, newName);
            this.FileName = newName;
        }

        public void Clear()
        {
            if (File.Exists(this.FileName)) File.Delete(this.FileName);
        }

        public void ConfigureTokenCache(ITokenCache tokenCache)
        {
            tokenCache.SetBeforeAccess(this.BeforeAccessNotification);
            tokenCache.SetAfterAccess(this.AfterAccessNotification);
        }

        private void BeforeAccessNotification(TokenCacheNotificationArgs args)
        {
            lock (FileLock)
            {
                if (File.Exists(this.FileName))
                {
                    try
                    {
                        byte[] encryptedData = File.ReadAllBytes(this.FileName);
                        args.TokenCache.DeserializeMsalV3(Unprotect(encryptedData));
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
                lock (FileLock)
                {
                    try
                    {
                        byte[] data = args.TokenCache.SerializeMsalV3();
                        File.WriteAllBytes(this.FileName, Protect(data));
                    }
                    catch { }
                }
            }
        }

        /// <summary>
        /// On Windows uses DPAPI (CurrentUser scope) to encrypt the cache file.
        /// On macOS / Linux the bytes are written without encryption; the file is
        /// protected by OS-level permissions (chmod 600 applied in the constructor).
        /// </summary>
        private static byte[] Protect(byte[] data) =>
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser)
                : data;

        private static byte[] Unprotect(byte[] data) =>
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? ProtectedData.Unprotect(data, null, DataProtectionScope.CurrentUser)
                : data;
    }
}
