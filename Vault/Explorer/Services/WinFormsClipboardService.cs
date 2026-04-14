using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;
using System.Windows.Forms;
using Microsoft.Vault.Explorer.Model.ContentTypes;

namespace Microsoft.Vault.Explorer.Services;

/// <summary>
/// Windows WinForms clipboard bridge.
/// Used as the IClipboardService during Phase 1-3 while WinForms still runs the app.
/// Deleted in Phase 5 when WinForms is removed.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WinFormsClipboardService : IClipboardService
{
    public Task SetTextAsync(string text)
    {
        var dataObj = new DataObject("Preferred DropEffect", DragDropEffects.Move);
        dataObj.SetData(DataFormats.Text, text);
        dataObj.SetData(DataFormats.UnicodeText, text);
        Clipboard.SetDataObject(dataObj, true);
        return Task.CompletedTask;
    }

    public Task SetFileAsync(string filePath)
    {
        var dataObj = new DataObject("Preferred DropEffect", DragDropEffects.Move);
        var sc = new StringCollection();
        sc.Add(filePath);
        dataObj.SetFileDropList(sc);
        Clipboard.SetDataObject(dataObj, true);
        return Task.CompletedTask;
    }

    public Task SetPayloadAsync(Microsoft.Vault.Explorer.Model.ClipboardPayload payload)
    {
        var dataObj = new DataObject("Preferred DropEffect", DragDropEffects.Move);
        if (payload.Text is not null)
            dataObj.SetData(DataFormats.UnicodeText, payload.Text);
        if (payload.FilePath is not null)
        {
            var sc = new StringCollection();
            sc.Add(payload.FilePath);
            dataObj.SetFileDropList(sc);
        }
        Clipboard.SetDataObject(dataObj, true);
        return Task.CompletedTask;
    }

    public Task SetHyperlinkAsync(string url, string name)
    {
        const string html = @"Version:0.9
StartHTML:<<<<<<<1
EndHTML:<<<<<<<2
StartFragment:<<<<<<<3
EndFragment:<<<<<<<4
SourceURL: {0}
<html>
<body>
<!--StartFragment-->
<a href='{0}'>{1}</a>
<!--EndFragment-->
</body>
</html>";
        var dataObj = new DataObject("Preferred DropEffect", DragDropEffects.Move);
        dataObj.SetData(DataFormats.Text, url);
        dataObj.SetData(DataFormats.UnicodeText, url);
        dataObj.SetData(DataFormats.Html, string.Format(html, url, name));

        var tempPath = Path.Combine(Path.GetTempPath(), name + ContentType.KeyVaultLink.ToExtension());
        File.WriteAllText(tempPath, $"[InternetShortcut]\r\nURL={url}\r\nIconIndex=47\r\nIconFile=%SystemRoot%\\system32\\SHELL32.dll");
        var sc = new StringCollection();
        sc.Add(tempPath);
        dataObj.SetFileDropList(sc);

        Clipboard.SetDataObject(dataObj, true);
        return Task.CompletedTask;
    }

    public void SpawnClearClipboardProcess(TimeSpan ttl, string md5)
    {
        var sInfo = new ProcessStartInfo(
            Path.Combine(Application.StartupPath, "ClearClipboard.exe"),
            $"{ttl} {md5}")
        {
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            UseShellExecute = false,
            LoadUserProfile = false,
        };
        Process.Start(sInfo);
    }
}
