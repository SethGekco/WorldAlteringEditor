using System;
#if WINDOWS
using System.Runtime.InteropServices;
#endif

namespace TSMapEditor;

// stride-2048 fork / DesktopGL: these are Windows-only native calls (gdi32/user32/kernel32). On
// other platforms they are compiled out and the public helpers return cross-platform-safe defaults.
public static class NativeMethods
{
#if WINDOWS
    [DllImport("gdi32.dll")]
    private static extern int GetDeviceCaps(IntPtr hdc, int nIndex);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    /// <summary>
    /// Logical pixels inch in X
    /// </summary>
    private const int LOGPIXELSX = 88;

    /// <summary>
    /// Logical pixels inch in Y
    /// </summary>
    private const int LOGPIXELSY = 90;


    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AllocConsole();

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FreeConsole();
#endif


    public static int GetScreenDPI()
    {
#if WINDOWS
        // This function currently only works on Windows
        IntPtr hdc = GetDC(IntPtr.Zero);
        return GetDeviceCaps(hdc, LOGPIXELSX);
#else
        // Default logical DPI; MonoGame/SDL handles HiDPI scaling on other platforms.
        return 96;
#endif
    }

    public static void CreateConsole()
    {
#if WINDOWS
        AllocConsole();
#endif
    }

    public static void DisableConsole()
    {
#if WINDOWS
        FreeConsole();
#endif
    }
}
