namespace Microsoft.Vault.Explorer.Controls.Lists.Favorites
{
    public static class FavoriteSecretUtil
    {
        private static FavoriteSecretsDictionary Store => AppSettings.Default.FavoriteSecretsDictionary;

        public static bool Contains(string vaultAlias, string secretName)
        {
            return Store.ContainsKey(vaultAlias) && Store[vaultAlias].ContainsKey(secretName);
        }

        public static void Add(string vaultAlias, string secretName)
        {
            if (!Store.ContainsKey(vaultAlias))
                Store.Add(vaultAlias, new FavoriteSecrets());

            Store[vaultAlias][secretName] = new FavoriteSecret();
            AppSettings.Default.Save();
        }

        public static void Remove(string vaultAlias, string secretName)
        {
            if (!Store.ContainsKey(vaultAlias)) return;

            var favorites = Store[vaultAlias];
            favorites.Remove(secretName);
            if (favorites.Count == 0)
                Store.Remove(vaultAlias);

            AppSettings.Default.Save();
        }
    }
}