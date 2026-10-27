namespace Shoko.BuildTools.Tasks;

/// <summary>
///   Writes a generated source file only when its text changed, so an
///   unchanged plugin is not recompiled on every build.
/// </summary>
internal static class GeneratedFile
{
    /// <summary>
    ///   Writes <paramref name="text" /> to <paramref name="path" /> unless
    ///   the file already holds exactly that text.
    /// </summary>
    /// <param name="path">The file to write.</param>
    /// <param name="text">The file's text.</param>
    /// <returns>Whether the file was written.</returns>
    public static bool Write(string path, string text)
    {
        if (File.Exists(path) && File.ReadAllText(path) == text)
            return false;

        File.WriteAllText(path, text);
        return true;
    }
}
