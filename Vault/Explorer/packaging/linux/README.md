# Linux packaging — taskbar icon

The Avalonia `<Window.Icon>` sets the window chrome icon, but on GNOME, KDE, and most other
Linux desktop environments the **taskbar/dock icon** is resolved from the matching `.desktop`
entry based on the window's `WM_CLASS`. Without an installed `.desktop` file the shell falls
back to a generic icon (often a gear).

## Install for the current user (manual test)

```bash
# 1. Copy the icon (512×512 PNG recommended)
mkdir -p ~/.local/share/icons/hicolor/512x512/apps
cp /path/to/repo/Vault/Explorer/VaultExplorer.png \
   ~/.local/share/icons/hicolor/512x512/apps/vault-explorer.png

# 2. Install the .desktop entry
mkdir -p ~/.local/share/applications
cp vault-explorer.desktop ~/.local/share/applications/vault-explorer.desktop

# 3. Point Exec= at your local build (edit the file after copying)
sed -i "s|Exec=VaultExplorer %u|Exec=$(pwd)/../../bin/Debug/net10.0/linux-x64/VaultExplorer %u|" \
   ~/.local/share/applications/vault-explorer.desktop

# 4. Refresh caches
update-desktop-database ~/.local/share/applications 2>/dev/null || true
gtk-update-icon-cache -t ~/.local/share/icons/hicolor 2>/dev/null || true
```

Log out and back in (or restart your shell) so the desktop environment picks up the new icon.

## System install (for packagers)

Install to `/usr/share/applications/vault-explorer.desktop` and
`/usr/share/icons/hicolor/512x512/apps/vault-explorer.png`. The `.desktop` `Exec=` line
should point to the installed binary path (typically `/usr/bin/VaultExplorer`).

## StartupWMClass

The `.desktop` file uses `StartupWMClass=VaultExplorer` which must match the window's
`WM_CLASS` (set by Avalonia from the assembly name). If you rename the assembly, update
this value and rebuild.
