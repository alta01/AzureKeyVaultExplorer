using System.Diagnostics;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;

namespace Microsoft.Vault.Explorer.Services;

/// <summary>
/// Cross-platform clipboard service using Avalonia's IClipboard API.
/// File drop operations that rely on OLE DataObject are Windows-only;
/// on other platforms only the text representation is set.
/// </summary>
public sealed class AvaloniaClipboardService : IClipboardService
{
    private IClipboard? Clipboard =>
        Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
            ? TopLevel.GetTopLevel(desktop.MainWindow)?.Clipboard
            : null;

    public async Task SetTextAsync(string text)
    {
        var cb = Clipboard;
        if (cb is not null)
            await cb.SetTextAsync(text);
    }

    public async Task SetFileAsync(string filePath)
    {
        // Avalonia supports file drop on Windows and macOS via DataObject.
        var dataObject = new DataObject();
        dataObject.Set(DataFormats.Files, new[] { filePath });
        var cb = Clipboard;
        if (cb is not null)
            await cb.SetDataObjectAsync(dataObject);
    }

    public async Task SetPayloadAsync(Microsoft.Vault.Explorer.Model.ClipboardPayload payload)
    {
        var dataObject = new DataObject();
        if (payload.Text is not null)
            dataObject.Set(DataFormats.Text, payload.Text);
        if (payload.FilePath is not null)
            dataObject.Set(DataFormats.Files, new[] { payload.FilePath });

        var cb = Clipboard;
        if (cb is not null)
            await cb.SetDataObjectAsync(dataObject);
    }

    public async Task SetHyperlinkAsync(string url, string name)
    {
        // Set plain text URL so cross-platform pasteboard works everywhere.
        // On Windows a richer OLE DataObject with HTML+file could be added via
        // platform-conditional code if needed in the future.
        var dataObject = new DataObject();
        dataObject.Set(DataFormats.Text, url);
        var cb = Clipboard;
        if (cb is not null)
            await cb.SetDataObjectAsync(dataObject);

        // Also write a .URL shortcut file to temp dir (Windows Explorer compatible).
        try
        {
            var tempPath = Path.Combine(Path.GetTempPath(), name + ".url");
            await File.WriteAllTextAsync(tempPath,
                $"[InternetShortcut]\r\nURL={url}\r\nIconIndex=47\r\nIconFile=%SystemRoot%\\system32\\SHELL32.dll");
        }
        catch
        {
            // Non-critical — a missing temp file is acceptable.
        }
    }

    public void SpawnClearClipboardProcess(TimeSpan ttl, string md5)
    {
        // ClearClipboard.exe lives alongside the main executable.
        var exeDir = Path.GetDirectoryName(Environment.ProcessPath) ?? AppDomain.CurrentDomain.BaseDirectory;

        // On Windows the helper is a .exe; on other platforms a platform-native binary or script.
        var helperName = OperatingSystem.IsWindows() ? "ClearClipboard.exe" : "ClearClipboard";
        var helperPath = Path.Combine(exeDir, helperName);

        if (!File.Exists(helperPath))
            return; // Helper not present — clipboard will not be auto-cleared.

        var sInfo = new ProcessStartInfo(helperPath, $"{ttl} {md5}")
        {
            CreateNoWindow = true,
            UseShellExecute = false,
        };
        Process.Start(sInfo);
    }
}
