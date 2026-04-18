# Claude Code instructions for this repo

## After every completion

After finishing any task, **push changes to the remote branch**:

```bash
git push origin <current-branch>
```

If the branch doesn't have an upstream yet, use `git push -u origin <branch>`.

## Branching

- Default branch: `avalonia-cross-platform`
- All changes require a PR — direct push to `avalonia-cross-platform` is blocked
- Create feature/fix branches from `avalonia-cross-platform`, not from `main`

## Building

```bash
dotnet build Vault/Explorer/VaultExplorer.csproj
```

Requires .NET 10 SDK. Must have 0 errors before committing.

## Running

```bash
dotnet run --project Vault/Explorer/VaultExplorer.csproj
```
