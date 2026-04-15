using Microsoft.Vault.Explorer.Model;

namespace Microsoft.Vault.Explorer.Services;

/// <summary>
/// Cross-platform clipboard abstraction.
/// Replaces direct System.Windows.Forms.Clipboard calls.
/// </summary>
public interface IClipboardService
{
    /// <summary>Sets plain text on the clipboard.</summary>
    Task SetTextAsync(string text);

    /// <summary>
    /// Drops a file path onto the clipboard as a file list entry
    /// (used for certificate export to clipboard).
    /// </summary>
    Task SetFileAsync(string filePath);

    /// <summary>
    /// Sets a hyperlink on the clipboard in HTML, Text, and .URL file formats.
    /// </summary>
    Task SetHyperlinkAsync(string url, string name);

    /// <summary>
    /// Places a <see cref="ClipboardPayload"/> onto the clipboard in one call,
    /// setting both text and file-drop data where applicable.
    /// </summary>
    Task SetPayloadAsync(ClipboardPayload payload);

    /// <summary>
    /// Spawns the ClearClipboard helper process to clear the clipboard after <paramref name="ttl"/>.
    /// The process only clears the clipboard if the current clipboard md5 still matches.
    /// </summary>
    void SpawnClearClipboardProcess(TimeSpan ttl, string md5);
}
