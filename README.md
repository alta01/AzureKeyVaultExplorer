# Azure Key Vault Explorer

**Cross-platform desktop app for browsing, editing, and managing Azure Key Vault secrets, keys, and certificates — now powered by Avalonia UI.**

> This repository is a fork of [reysic/AzureKeyVaultExplorer](https://github.com/reysic/AzureKeyVaultExplorer), which originated from [microsoft/AzureKeyVaultExplorer](https://github.com/microsoft/AzureKeyVaultExplorer).

Original Authors: Eli Zeitlin, Gokhan Ozhan, Anna Zeitlin

---

## Table of Contents

* [Key features](#key-features)
* [Getting started](#getting-started)
* [How to add or open vaults](#how-to-add-or-open-vaults)
* [Keyboard shortcuts](#keyboard-shortcuts)
* [Configuration](#configuration)
  * [Vaults.json](#vaultsjson)
  * [SecretKinds.json](#secretkindsjson)
  * [CustomTags.json](#customtagsjson)
  * [VaultAliases.json](#vaultaliasesjson)
  * [Settings file](#settings-file)
* [Themes](#themes)
* [Contributing](#contributing)
  * [Building](#building)
  * [TODOs](#todos)

---

## Key features

* **Cross-platform** — runs on Windows, macOS, and Linux (net10.0 / Avalonia UI 11)
* **Vault tabs** — open multiple vaults simultaneously and switch between them instantly
* **Pre-connection check** — verifies vault network accessibility and permissions before connecting
* **Material Design icons** — crisp vector icons at any DPI (Material.Icons.Avalonia)
* **4 built-in themes** — Arctic Frost (light default), Ocean Depths, Modern Minimalist, Midnight Galaxy; live preview in Settings
* Single sign-on via system browser (MSAL v4); prompted at most once per session
* Supports certificate, client-credential, and interactive user authentication
* Single or dual-vault HA/DR configuration
* Upload and download certificate (.pfx, .p12, .cer) files
* Drag-and-drop secrets and certificates into the vault list
* Copy secret to clipboard with auto-clear after a configurable delay
* Copy as environment variable, Docker `--env`, or Kubernetes YAML block
* Export all or selected items to TSV
* Favorite items per vault
* Browse vaults from your Azure subscriptions (ARM)
* Fast regex-based search / filter
* Customizable secret kinds with regex validation and tag schemas
* Secret and certificate revision history with one-click rollback
* Disable / expire items in one click
* Color-coded expiry warnings (configurable thresholds)

---

## Getting started

### Requirements

| | |
|---|---|
| Runtime | [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) |
| Platforms | Windows 10+, macOS 12+, Linux (X11 or Wayland) |

### Run from source

```bash
git clone https://github.com/alta01/AzureKeyVaultExplorer.git
cd AzureKeyVaultExplorer
dotnet run --project Vault/Explorer/VaultExplorer.csproj
```

### Build a self-contained executable

```bash
# Linux x64
dotnet publish Vault/Explorer/VaultExplorer.csproj \
  -c Release -r linux-x64 --self-contained -o dist/linux-x64

# Windows x64
dotnet publish Vault/Explorer/VaultExplorer.csproj \
  -c Release -r win-x64 --self-contained -o dist/win-x64

# macOS arm64 (Apple Silicon)
dotnet publish Vault/Explorer/VaultExplorer.csproj \
  -c Release -r osx-arm64 --self-contained -o dist/osx-arm64
```

---

## How to add or open vaults

There are two ways to connect to a vault:

**1. Pick from subscription (recommended for new users)**

Open the vault dropdown at the top of the main window and select **"Pick vault from subscription..."**. Sign in with your Azure account, choose a subscription, and select a vault. The app will run a quick connectivity check before enabling OK.

**2. VaultAliases.json (recommended for teams)**

Define named vault aliases in a `VaultAliases.json` file and point Settings to its folder. This gives full control over vault access credentials, secret kinds, and dual-vault HA setups. See [VaultAliases.json](#vaultaliasesjson) below.

---

## Keyboard shortcuts

| Key | Action |
|-----|--------|
| F5 | Refresh vault |
| Delete | Delete selected item(s) |
| Enter | Edit selected item |
| Ctrl+F | Focus search box |
| Ctrl+C | Copy value to clipboard |

---

## Configuration

Settings are stored in JSON format at:

| Platform | Path |
|----------|------|
| Linux / macOS | `~/.config/VaultExplorerNext/settings.json` |
| Windows | `%APPDATA%\VaultExplorerNext\settings.json` |

Vault configuration files are read from the **JSON configuration files root** path set in Settings (default: app directory).

### Vaults.json

Defines vault credentials. Optional if your current account already has access. Supports three access types:

* `VaultAccessClientCertificate` — authenticates with a certificate thumbprint
* `VaultAccessClientCredential` — authenticates with a client secret
* `VaultAccessUserInteractive` — authenticates via browser (default)

Use `$id` / `$ref` for dual-vault HA configurations:

```json
{
  "myVault": {
    "$id": "1",
    "ReadOnly": [],
    "ReadWrite": [
      {
        "$type": "Microsoft.Vault.Library.VaultAccessUserInteractive, Microsoft.Vault.Library",
        "DomainHint": "contoso.com"
      }
    ]
  },
  "myVault-dr": { "$ref": "1" }
}
```

### VaultAliases.json

Named aliases shown in the main vault dropdown:

```json
[
  {
    "Alias": "Production",
    "VaultNames": [ "my-prod-vault" ],
    "SecretKinds": [ "Custom", "Service.Secret" ]
  },
  {
    "Alias": "Production (HA)",
    "VaultNames": [ "my-prod-vault", "my-prod-vault-dr" ]
  }
]
```

### SecretKinds.json

Regex-validated secret types with optional tag schemas and expiry defaults. Each kind can define:

* `NameRegex` — valid secret name pattern
* `ValueRegex` — valid value pattern; named groups are auto-extracted to tags
* `ValueTemplate` — placeholder shown in the editor
* `RequiredCustomTags` / `OptionalCustomTags` — references to `CustomTags.json`
* `DefaultExpiration` / `MaxExpiration` — timespan strings (`"90.00:00:00"` = 90 days)

### CustomTags.json

Tag definitions referenced by SecretKinds:

```json
{
  "Environment": {
    "Name": "Environment",
    "DefaultValue": "dev",
    "ValueRegex": "^(dev|staging|prod)$",
    "ValueList": ["dev", "staging", "prod"]
  }
}
```

### Settings file

User preferences are saved automatically. Key settings:

| Setting | Default | Description |
|---------|---------|-------------|
| Theme | Arctic Frost | UI color theme |
| Copy-to-clipboard TTL | 12 seconds | How long secret stays in clipboard |
| Expiry warning period | 30 days | When items turn yellow |
| Expired color | Red | Color for expired items |
| Expiring soon color | Orange | Color for items near expiry |
| Disabled color | Gray | Color for disabled items |
| JSON configuration files root | _(app dir)_ | Folder containing `*.json` config files |

---

## Themes

Four built-in themes, switchable live from **Settings**:

| Theme | Variant | Description |
|-------|---------|-------------|
| Arctic Frost | Light | Clean white/blue — the default |
| Modern Minimalist | Light | Neutral gray, low-chrome |
| Ocean Depths | Dark | Deep teal/blue |
| Midnight Galaxy | Dark | Dark purple/indigo |

---

## Contributing

### Building

Requirements: [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0). No Visual Studio required.

```bash
# Full solution
dotnet build AzureKeyVaultExplorer.sln

# Explorer project only
dotnet build Vault/Explorer/VaultExplorer.csproj
```

The migration from WinForms to Avalonia lives in the PR from `claude/avalonia-migration-effort-pILGp` into this branch (`avalonia-cross-platform`).

### Tech stack

| Layer | Technology |
|-------|-----------|
| UI | [Avalonia UI 11](https://avaloniaui.net/) + ReactiveUI + DynamicData |
| MVVM | ReactiveUI (`ReactiveObject`, `ReactiveCommand`) |
| Icons | Material.Icons.Avalonia 2.1.0 |
| Auth | Microsoft.Identity.Client (MSAL v4) — system browser OAuth |
| Azure SDK | Azure.Security.KeyVault.Secrets/Certificates, Azure.ResourceManager.KeyVault |
| Target | `net10.0` (cross-platform) |

### TODOs

* Wire up remaining WinForms features not yet in the Avalonia port:
  * Secret compression (gzip + base64)
  * `vault://` protocol handler registration (Windows registry / Linux `.desktop`)
  * PowerShell session launch (was removed upstream, no replacement yet)
* Add automated UI tests
* Publish binaries via GitHub Releases
