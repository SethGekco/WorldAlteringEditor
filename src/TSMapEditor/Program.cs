using System;
#if WINDOWS
using System.Windows.Forms;
#endif
using TSMapEditor.Rendering;

namespace TSMapEditor;

static class Program
{
    public static string[] args;

    /// <summary>
    /// The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main(string[] args)
    {
        Program.args = args;

        // stride-2048 fork / DesktopGL: the WinForms-based DPI and thread-exception setup is
        // Windows-only. On other platforms MonoGame/SDL handles DPI and we rely on the AppDomain
        // unhandled-exception handler below.
#if WINDOWS
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);

        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
        Application.ThreadException += Application_ThreadException;
#endif
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

#if WINDOWS
        Environment.CurrentDirectory = Application.StartupPath.Replace('\\', '/');
#else
        Environment.CurrentDirectory = AppContext.BaseDirectory;
#endif
        new GameClass().Run();
    }

    private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        HandleException((Exception)e.ExceptionObject);
    }

#if WINDOWS
    private static void Application_ThreadException(object sender, System.Threading.ThreadExceptionEventArgs e)
    {
        HandleException(e.Exception);
    }
#endif

    private static void HandleException(Exception ex)
    {
        string message = "The map editor failed to launch.\r\n\r\nReason: " + ex.Message + "\r\n\r\n Stack trace: " + ex.StackTrace;
#if WINDOWS
        MessageBox.Show(message);
#else
        Console.Error.WriteLine(message);
#endif
    }

    public static void DisableExceptionHandler()
    {
#if WINDOWS
        Application.ThreadException -= Application_ThreadException;
#endif
        AppDomain.CurrentDomain.UnhandledException -= CurrentDomain_UnhandledException;
    }
}
