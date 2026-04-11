# AGENTS.md — Azure Key Vault Explorer

This file provides context for AI coding agents working in this repository.

## What this project is

Azure Key Vault Explorer is a Windows desktop application (.NET 10 / WinForms) for browsing,
editing, and managing secrets, keys, and certificates stored in Azure Key Vault. It supports
ClickOnce deployment so end users install and auto-update via a hosted manifest URL.

This repository is a fork of [reysic/AzureKeyVaultExplorer](https://github.com/reysic/AzureKeyVaultExplorer),
which originated from [microsoft/AzureKeyVaultExplorer](https://github.com/microsoft/AzureKeyVaultExplorer).
Changes developed here are intended to be contributed back upstream via a PR to reysic's fork.

## Tech stack

| Layer | Technology |
|---|---|
| UI | Windows Forms (.NET 10) |
| Auth | Microsoft.Identity.Client (MSAL v4) — browser-based OAuth |
| Azure SDK | Microsoft.Azure.Management.KeyVault (ARM), Microsoft.Azure.KeyVault (data plane) |
| Publishing | ClickOnce via MSBuild `/target:publish` with `ClickOnceProfile.pubxml` |
| CI | GitHub Actions (`.github/workflows/release.yml`), triggered by `v*` tags |

## Repository layout

```
AzureKeyVaultExplorer.sln       Solution file
Vault/
  Explorer/                     Main WinForms app (VaultExplorer.csproj)
    Common/                     Helpers: ActivationUri, Utils, UxOperation
    Config/                     VaultConfigurationManager + JSON config templates
    Controls/                   ListViews, custom WinForms controls
    Dialogs/
      Subscriptions/            SubscriptionsManagerDialog — vault picker via ARM
      Secrets/                  SecretDialog
      Certificates/             CertificateDialog
      Settings/                 SettingsDialog
    Model/                      Domain objects: PropObjects, ContentTypes, Tags, Aliases
    Globals.cs                  All global URL constants (GitHub, ClickOnce install URL)
    MainForm.cs                 Main window; hosts vault dropdown and secret list
    Program.cs                  Entry point; handles ClickOnce activation URI
  Library/                      Vault access abstractions (VaultAccess*, VaultConfig, etc.)
  Core/                         Utilities shared across Library and Explorer
  ClearClipboard/               Companion exe to clear clipboard after copy-secret
  Build/                        T4 templates and version props
.github/workflows/release.yml  CI release workflow
release.ps1                     Local / CI publish script (requires MSBuild 17.14+)
release.md                      Step-by-step release instructions
tag_and_push.ps1               Creates and pushes a `v*` tag to trigger release
```

## Key constants — always keep these pointing to reysic

`Vault/Explorer/Globals.cs` holds every URL string the app uses at runtime:

```csharp
OnlineActivationUri  = "https://reysic.github.io/AzureKeyVaultExplorer/VaultExplorer.application"
GitHubUrl            = "https://github.com/reysic/AzureKeyVaultExplorer"
GitHubIssuesUrl      = "https://github.com/reysic/AzureKeyVaultExplorer/issues"
ActivationUrl        = "https://reysic.github.io/AzureKeyVaultExplorer/VaultExplorer.application"
```

`Vault/Explorer/Properties/PublishProfiles/ClickOnceProfile.pubxml` holds the ClickOnce
`InstallUrl`, `ErrorReportUrl`, and `SupportUrl` — these must also point to reysic.

## How to build locally

```
dotnet build AzureKeyVaultExplorer.sln
```

Requirements: .NET 10 SDK (`dotnet --list-sdks` should show `10.x`).

## How to publish (ClickOnce release)

See `release.md`. Short version:

1. Run `.\tag_and_push.ps1` — creates and pushes a `v*` tag.
2. GitHub Actions runs `release.ps1` which publishes with MSBuild and pushes output to `gh-pages`.
3. Users install / update from `https://reysic.github.io/AzureKeyVaultExplorer`.

For local-only publish validation (no gh-pages push):
```powershell
.\release.ps1 -OnlyBuild
```

Requires: **Visual Studio 2022 17.14+** (MSBuild 17.14+ for .NET 10 ClickOnce).

## Vault picker dialog (SubscriptionsManagerDialog)

The "Pick vault from subscription..." flow in `MainForm.cs` opens
`Vault/Explorer/Dialogs/Subscriptions/SubscriptionsManagerDialog.cs`.

Key behaviors to be aware of when editing it:

- **Account → Tenant → Subscription → Vault** — cascading async selection.
- `uxButtonOK` starts **disabled**; it is only enabled after `Vaults.GetAsync` succeeds in
  `uxListViewVaults_SelectedIndexChanged`. A try-catch wraps this call — errors show a warning
  MessageBox but leave the dialog open so the user can pick a different vault.
- The onboarding prompt (no saved accounts) fires from the `Shown` event, not the constructor,
  so the form is fully rendered before any MessageBox appears.
- `MinimumSize` is set in the Designer to prevent the window from being resized so small that
  the OK/Cancel buttons are clipped.

## UxOperation pattern

`Common/UxOperation.cs` is used with `using` to show progress while async work runs:

```csharp
using (var op = this.NewUxOperationWithProgress(controlsToDisable))
{
    // async work here; op.CancellationToken is available
}
```

`Dispose()` re-enables controls, hides the progress bar, and resets the cursor.

## Configuration files (user-editable)

Located in the app's install folder or a user-specified "Root location":

| File | Purpose |
|---|---|
| `Vaults.json` | Vault credential definitions |
| `VaultAliases.json` | Named vault aliases shown in the main dropdown |
| `SecretKinds.json` | Regex-validated secret types with tag schemas |
| `CustomTags.json` | Tag definitions referenced by SecretKinds |

## Known open items

- Deprecated Azure SDK packages (Microsoft.Azure.KeyVault, Microsoft.Azure.Management.KeyVault,
  Microsoft.IdentityModel.Clients.ActiveDirectory, etc.) — migration to Track 2 non-preview
  packages is a pending backlog item.
- PowerShell integration was removed (does not work with .NET 8+).

## Coding conventions

- Namespace prefix: `Microsoft.Vault.*`
- `async void` is used only for WinForms event handlers; wrap async body in try-catch.
- All user-visible strings are inline (no resource file abstraction needed for this tool).
- Designer-generated code lives in `*.Designer.cs`; do not edit `.resx` binary sections manually.
