namespace Microsoft.Vault.Explorer.Model.Files.Secrets
{
    using System;
    using System.Runtime.InteropServices;
    using System.Security.Cryptography;
    using System.Text;
    using Microsoft.AspNetCore.DataProtection;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Vault.Explorer.Common;
    using Newtonsoft.Json;

    [JsonObject]
    public abstract class KeyVaultFile<T> where T : class
    {
        [JsonProperty]
        public readonly string CreatedBy;

        [JsonProperty]
        public readonly DateTimeOffset CreationTime;

        [JsonProperty]
        public readonly byte[] Data;

        // Resolved at call time — App.Services is available after OnFrameworkInitializationCompleted
        private static IDataProtector Protector =>
            App.Services.GetRequiredService<IDataProtector>();

        [JsonConstructor]
        public KeyVaultFile()
        {
        }

        protected KeyVaultFile(T obj)
        {
            this.CreatedBy = Globals.DefaultUserName;
            this.CreationTime = DateTimeOffset.UtcNow;
            var plaintext = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(obj, Formatting.Indented));
            this.Data = Protector.Protect(plaintext);
        }

        private string GetValueForDeserialization()
        {
            try
            {
                return Encoding.UTF8.GetString(Protector.Unprotect(this.Data));
            }
            catch (CryptographicException) when (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Legacy: file was encrypted with Windows DPAPI before cross-platform migration.
                // Silently migrate — next save will write in the new format.
                return Encoding.UTF8.GetString(
                    ProtectedData.Unprotect(this.Data, null, DataProtectionScope.CurrentUser));
            }
        }

        public T Deserialize() => JsonConvert.DeserializeObject<T>(this.GetValueForDeserialization());

        public string Serialize()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("// --------------------------------------------------------------------------------------");
            sb.AppendLine($"// {Globals.AppName} encrypted {typeof(T).Name}");
            sb.AppendLine("// Do not edit manually!!!");
            sb.AppendLine("// This file is encrypted and can only be decrypted by the account that created it");
            sb.AppendLine("// --------------------------------------------------------------------------------------");
            sb.AppendLine();
            sb.Append(JsonConvert.SerializeObject(this, Formatting.Indented));
            return sb.ToString();
        }
    }
}