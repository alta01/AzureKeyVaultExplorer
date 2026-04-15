namespace Microsoft.Vault.Explorer
{
    using System;

    internal static class Globals
    {
        public const string ProductName = "VaultExplorerNext";

        public const string AppName = "Azure Key Vault Explorer";

        public const string GitHubUrl = "https://github.com/alta01/AzureKeyVaultExplorer";
        public const string GitHubIssuesUrl = "https://github.com/alta01/AzureKeyVaultExplorer/issues";

        // Legacy vault:// deep-link prefix (no longer used for ClickOnce; kept for URL parsing)
        public const string OnlineActivationUri = "https://alta01.github.io/AzureKeyVaultExplorer/VaultExplorer.application";
        public const string ActivationUrl = OnlineActivationUri;

        public static string DefaultUserName = Environment.UserName;
    }
}
