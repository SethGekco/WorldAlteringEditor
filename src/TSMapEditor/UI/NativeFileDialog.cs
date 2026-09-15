using System;
using System.Diagnostics;
using Rampastring.Tools;

namespace TSMapEditor.UI;

/// <summary>
/// stride-2048 fork / DesktopGL: cross-platform native file/folder pickers for platforms without
/// the Windows-only System.Windows.Forms file dialogs. On Linux this shells out to the desktop's
/// portal helper (kdialog on KDE, zenity on GNOME/others). Returns null when the tool is missing or
/// the user cancels. The calling game loop blocks while the modal dialog is open, matching the
/// behaviour of the WinForms dialogs on Windows.
/// </summary>
public static class NativeFileDialog
{
    public static bool IsAvailable => FindTool() != null;

    /// <summary>
    /// Shows an "open file" dialog. <paramref name="patterns"/> is a space-separated list of glob
    /// patterns (e.g. "*.map *.mpr *.yrm"). Returns the selected path, or null.
    /// </summary>
    public static string OpenFile(string title, string initialDirectory, string patterns, string filterDescription)
    {
        string tool = FindTool();
        if (tool == null)
            return null;

        string startDir = NormalizeStartDir(initialDirectory);

        if (tool.EndsWith("kdialog"))
        {
            // kdialog filter format: "*.map *.mpr|Description"
            string filter = string.IsNullOrWhiteSpace(patterns) ? null : $"{patterns}|{filterDescription}";
            return RunTool(tool, filter == null
                ? new[] { "--title", title, "--getopenfilename", startDir }
                : new[] { "--title", title, "--getopenfilename", startDir, filter });
        }

        // zenity
        var args = new System.Collections.Generic.List<string> { "--file-selection", $"--title={title}" };
        if (!string.IsNullOrWhiteSpace(startDir))
            args.Add($"--filename={startDir}/");
        if (!string.IsNullOrWhiteSpace(patterns))
            args.Add($"--file-filter={filterDescription} | {patterns}");
        return RunTool(tool, args.ToArray());
    }

    /// <summary>
    /// Shows a "select folder" dialog. Returns the selected directory path, or null.
    /// </summary>
    public static string SelectFolder(string title, string initialDirectory)
    {
        string tool = FindTool();
        if (tool == null)
            return null;

        string startDir = NormalizeStartDir(initialDirectory);

        if (tool.EndsWith("kdialog"))
            return RunTool(tool, new[] { "--title", title, "--getexistingdirectory", startDir });

        // zenity
        var args = new System.Collections.Generic.List<string> { "--file-selection", "--directory", $"--title={title}" };
        if (!string.IsNullOrWhiteSpace(startDir))
            args.Add($"--filename={startDir}/");
        return RunTool(tool, args.ToArray());
    }

    private static string NormalizeStartDir(string dir)
    {
        if (string.IsNullOrWhiteSpace(dir))
            return string.Empty;

        try
        {
            if (System.IO.File.Exists(dir))
                return System.IO.Path.GetDirectoryName(dir);
            if (System.IO.Directory.Exists(dir))
                return dir;
            string parent = System.IO.Path.GetDirectoryName(dir);
            return System.IO.Directory.Exists(parent) ? parent : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string cachedTool;
    private static bool toolSearched;

    private static string FindTool()
    {
        if (toolSearched)
            return cachedTool;

        toolSearched = true;
        foreach (string candidate in new[] { "kdialog", "zenity" })
        {
            foreach (string dir in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(':'))
            {
                if (string.IsNullOrWhiteSpace(dir))
                    continue;

                string full = System.IO.Path.Combine(dir, candidate);
                if (System.IO.File.Exists(full))
                {
                    cachedTool = full;
                    return cachedTool;
                }
            }
        }

        return cachedTool;
    }

    private static string RunTool(string tool, string[] args)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = tool,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            foreach (string a in args)
                psi.ArgumentList.Add(a);

            using var process = Process.Start(psi);
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            // Both tools return non-zero and empty output on cancel.
            if (process.ExitCode != 0)
                return null;

            output = output.Trim();
            return string.IsNullOrWhiteSpace(output) ? null : output;
        }
        catch (Exception ex)
        {
            Logger.Log("NativeFileDialog: failed to run " + tool + ": " + ex.Message);
            return null;
        }
    }
}
