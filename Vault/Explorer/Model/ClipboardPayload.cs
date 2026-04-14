namespace Microsoft.Vault.Explorer.Model;

/// <summary>
/// Cross-platform clipboard payload returned by PropertyObject.GetClipboardValue().
/// Replaces System.Windows.Forms.DataObject as the return type.
/// The IClipboardService implementation decides how to put this onto the actual clipboard.
/// </summary>
/// <param name="Text">Plain text to place on the clipboard (secret value, cert text, URL).</param>
/// <param name="FilePath">
/// Optional path to a temporary file that should also be placed on the clipboard as a file-drop
/// (used for certificate export and .kv-link files). Null if not applicable.
/// </param>
public record ClipboardPayload(string? Text, string? FilePath = null);
