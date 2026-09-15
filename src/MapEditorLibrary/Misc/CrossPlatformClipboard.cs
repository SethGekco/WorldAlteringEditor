using System.Collections.Generic;

namespace MapEditorLibrary.Misc;

/// <summary>
/// Cross-platform replacement for the custom-format data clipboard operations that previously used
/// <c>System.Windows.Forms.Clipboard</c> (which is Windows-only and unavailable on the DesktopGL
/// build).
///
/// The editor uses the clipboard to copy/paste structured editor data (e.g. triggers) as raw byte
/// arrays keyed by a private format string. This implementation keeps that data in-process. That
/// covers copy/paste within a single running editor instance; cross-process copy/paste between two
/// separate editor windows is not supported here (it can be layered on later via the OS text
/// clipboard by base64-encoding the payload).
/// </summary>
public static class CrossPlatformClipboard
{
    private static readonly Dictionary<string, byte[]> store = new();

    public static void SetData(string format, byte[] data)
    {
        store[format] = data;
    }

    public static byte[] GetData(string format)
    {
        return store.TryGetValue(format, out byte[] data) ? data : null;
    }

    public static bool ContainsData(string format)
    {
        return store.ContainsKey(format) && store[format] != null;
    }
}
