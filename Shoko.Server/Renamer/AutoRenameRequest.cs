
#nullable enable
namespace Shoko.Server.Renamer;

/// <summary>
/// Represents a request to automatically rename a file.
/// </summary>
public record AutoRenameRequest
{
    /// <summary>
    /// Indicates whether the result should be a preview of the
    /// relocation.
    /// </summary>
    public bool Preview = false;

    /// <summary>
    /// The name of the renaming script to use. Leave blank to use the
    /// default script.
    /// </summary>
    public string? ScriptName = null;

    /// <summary>
    /// Do the rename operation.
    /// </summary>
    public bool Rename = true;
}
