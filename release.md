# Release Process (ClickOnce + GitHub Pages)

## Prerequisites

1. GitHub Pages is enabled for this repository and publishes from branch `gh-pages`.
2. The release workflow has `contents: write` permission (configured in `.github/workflows/release.yml`).
3. Repository has a valid ClickOnce publish profile (`Vault/Explorer/Properties/PublishProfiles/ClickOnceProfile.pubxml`).
4. For local publishing, use **Visual Studio 2022 17.14+** (MSBuild requirement for .NET 10 ClickOnce).

## Automated release flow

1. Tag the commit you want to release (script helper):

   ```powershell
   .\tag_and_push.ps1
   ```

   This creates and pushes a `v*` tag, which triggers `.github/workflows/release.yml`.

2. Workflow executes `release.ps1`:
   - restores and publishes VaultExplorer with the ClickOnce profile
   - uses tag version as `ApplicationVersion`
   - updates `gh-pages` branch with `Application Files` and `VaultExplorer.application`
   - initializes `gh-pages` automatically if it does not yet exist
   - skips commit/push when no deployment content changed

3. Users install/update from:

   ```text
   https://alta01.github.io/AzureKeyVaultExplorer
   ```

## Manual fallback

If needed, run locally:

```powershell
.\release.ps1
```

Build-only validation (no gh-pages push):

```powershell
.\release.ps1 -OnlyBuild
```
