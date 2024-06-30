using System.Diagnostics.CodeAnalysis;

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
    public bool Preview { get; set; } = false;

    /// <summary>
    /// Indicates that the request contains a body. It should not be allowed to
    /// run if <see cref="Preview"/> is not set to true.
    /// </summary>
    [MemberNotNullWhen(true, nameof(RenamerName))]
    [MemberNotNullWhen(false, nameof(ScriptID))]
    public bool ContainsBody => !string.IsNullOrEmpty(RenamerName) && !ScriptID.HasValue;

    /// <summary>
    /// The id of the renaming script to use. Omit to use the
    /// default script or the provided <see cref="RenamerName"/> and/or
    /// <see cref="ScriptBody"/>.
    /// </summary>
    public int? ScriptID { get; set; } = null;

    /// <summary>
    /// The name of the renamer to use for previewing changes. Trying to use
    /// this without <see cref="Preview"/> set to true will result in an error.
    /// </summary>
    public string? RenamerName { get; set; } = null;

    /// <summary>
    /// The script body to use with the renamer for previewing changes. Will be
    /// ignored if <see cref="RenamerName"/> is not set.
    /// </summary>
    public string? ScriptBody { get; set; } = null;

    /// <summary>
    /// Do the rename operation.
    /// </summary>
    public bool Rename { get; set; } = true;
}
