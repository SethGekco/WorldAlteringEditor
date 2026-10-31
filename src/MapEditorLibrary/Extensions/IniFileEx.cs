using System.Text;
using Rampastring.Tools;
using MapEditorLibrary.CCEngine;

namespace MapEditorLibrary.Extensions;

/// <summary>
/// IniFile with support for Ares #include and Phobos $Include and $Inherits.
/// </summary>
public class IniFileEx: IniFile
{
    private static readonly string AresIncludeSection = "#include";
    private static readonly string PhobosIncludeSection = "$Include";
    private static readonly string PhobosInheritsSection = "$Inherits";

    private readonly HashSet<IniSection> alreadyInheritedSections = new();

    public IniFileEx() : base() { }

    public IniFileEx(string fileName) : base(SanitizeFileOrEmpty(fileName))
    {
        FileName = fileName;
    }

    public IniFileEx(string filePath, CCFileManager ccFileManager) : base(SanitizeFileOrEmpty(filePath))
    {
        FileName = filePath;
        Include(ccFileManager);
        Inherit();
    }

    public IniFileEx(string filePath, Encoding encoding, CCFileManager ccFileManager) : base(SanitizeFileOrEmpty(filePath), encoding)
    {
        FileName = filePath;
        Include(ccFileManager);
        Inherit();
    }

    public IniFileEx(Stream stream, CCFileManager ccFileManager) : base(SanitizeStream(stream))
    {
        Include(ccFileManager);
        Inherit();
    }

    public IniFileEx(Stream stream, Encoding encoding, CCFileManager ccFileManager) : base(SanitizeStream(stream), encoding)
    {
        Include(ccFileManager);
        Inherit();
    }

    // stride-2048 fork: the game's own INI parser tolerates malformed lines (seen in the
    // wild: a section header missing its closing bracket, "[Host.Types=GGI"), but
    // Rampastring.Tools throws an IniParseException and the editor dies before showing
    // anything. Comment such lines out before parsing, with a logged warning, so the
    // editor is at least as lenient as the game. The source file is never modified.
    private static MemoryStream SanitizeFileOrEmpty(string filePath)
    {
        // Preserve Rampastring's missing-file semantics (empty INI, no exception).
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            return new MemoryStream();

        return SanitizeIniBytes(File.ReadAllBytes(filePath), filePath);
    }

    private static MemoryStream SanitizeStream(Stream stream)
    {
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return SanitizeIniBytes(buffer.ToArray(), "(stream)");
    }

    /// <summary>
    /// Returns the INI contents with any section-header line that has no closing
    /// bracket commented out. Works on raw bytes so the file's text encoding is
    /// passed through untouched; if nothing is malformed, the input is returned as-is.
    /// </summary>
    private static MemoryStream SanitizeIniBytes(byte[] bytes, string sourceName)
    {
        MemoryStream output = null;
        int pos = 0;
        int copiedUpTo = 0;
        int lineNumber = 0;

        while (pos < bytes.Length)
        {
            lineNumber++;
            int newline = Array.IndexOf(bytes, (byte)'\n', pos);
            int lineEnd = newline < 0 ? bytes.Length : newline + 1;

            int c = pos;
            while (c < lineEnd && (bytes[c] == (byte)' ' || bytes[c] == (byte)'\t'))
                c++;

            if (c < lineEnd && bytes[c] == (byte)'[')
            {
                bool closed = false;
                for (int i = c + 1; i < lineEnd; i++)
                {
                    if (bytes[i] == (byte)']')
                    {
                        closed = true;
                        break;
                    }
                    if (bytes[i] == (byte)'\r')
                        break;
                }

                if (!closed)
                {
                    output ??= new MemoryStream(bytes.Length + 16);
                    output.Write(bytes, copiedUpTo, pos - copiedUpTo);
                    output.WriteByte((byte)';');
                    copiedUpTo = pos;
                    Logger.Log($"IniFileEx: ignoring malformed INI line {lineNumber} in {sourceName} (section header has no closing bracket).");
                }
            }

            pos = lineEnd;
        }

        if (output == null)
            return new MemoryStream(bytes, 0, bytes.Length, writable: false);

        output.Write(bytes, copiedUpTo, bytes.Length - copiedUpTo);
        output.Position = 0;
        return output;
    }

    /// <summary>
    /// Creates an INI file from a directory + path or from MIX file system.
    /// </summary>
    /// <param name="filePath">Path to file or file name inside MIX file system.</param>
    /// <param name="gameDirectory">The path to the game directory.</param>
    /// <param name="ccFileManager">File manager object holding MIXes.</param>
    /// <param name="returnNullOnNotFound">Whether to return null or an empty INI object when the INI file is not found.</param>
    public static IniFileEx FromPathOrMix(string filePath, string gameDirectory, CCFileManager ccFileManager, bool returnNullOnNotFound = false)
    {
        if (filePath.Length == 0)
        {
            if (returnNullOnNotFound)
                return null;

            return new();
        }

        string iniPath = Path.Combine(gameDirectory, filePath);
        if (File.Exists(iniPath))
        {
            Logger.Log("INI file " + filePath + " found from " + gameDirectory);
            return new(iniPath, ccFileManager);
        }

        var iniBytes = ccFileManager.LoadFile(filePath);
        if (iniBytes != null)
            return new(new MemoryStream(iniBytes), ccFileManager);

        if (returnNullOnNotFound)
            return null;

        return new();
    }

    /// <summary>
    /// Includes all base INI files into this file.
    /// </summary>
    public void Include(CCFileManager ccFileManager)
    {
        if (!Constants.EnableIniInclude)
            return;

        string sectionName;
        if (SectionExists(PhobosIncludeSection))
            sectionName = PhobosIncludeSection;
        else if (SectionExists(AresIncludeSection))
            sectionName = AresIncludeSection;
        else
            return;

        foreach (var pair in GetSection(sectionName).Keys)
        {
            string directory = FileName != null ? SafePath.CombineFilePath(SafePath.GetFileDirectoryName(FileName)) : "";
            IniFileEx includedIni = FromPathOrMix(pair.Value, directory, ccFileManager);
            ConsolidateIniFiles(includedIni, this);
            Sections = includedIni.Sections;
        }
    }

    /// <summary>
    /// For each section, inherits all missing keys from listed parents.
    /// </summary>
    public void Inherit()
    {
        if (!Constants.EnableIniInheritance)
            return;

        foreach (var section in Sections)
            InheritFromParent(section);

        alreadyInheritedSections.Clear();
    }

    private void InheritFromParent(IniSection section)
    {
        // If this section has already been processed, exit.
        if (alreadyInheritedSections.Contains(section))
            return;

        // If this section has no parents, mark as processed and exit.
        if (!section.KeyExists(PhobosInheritsSection))
        {
            alreadyInheritedSections.Add(section);
            return;
        }

        // Run recursively.
        foreach (var parentName in section.GetListValue<string>(PhobosInheritsSection, ',', x => x))
        {
            var parent = Sections.Find(s => s.SectionName == parentName);
            if (parent == null)
                continue;

            InheritFromParent(parent);

            foreach (var pair in parent.Keys)
            {
                if (!section.KeyExists(pair.Key))
                    section.AddKey(pair.Key, pair.Value);
            }
        }
        alreadyInheritedSections.Add(section);
    }
}
