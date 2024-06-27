using System.Diagnostics.CodeAnalysis;
using Shoko.Server.Models;

#nullable enable
namespace Shoko.Server.Renamer;

/// <summary>
/// Represents a request to automatically rename a file.
/// </summary>
public record AutoRelocateRequest
{
    /// <summary>
    /// Indicates whether the result should be a preview of the
    /// relocation.
    /// </summary>
    [MemberNotNullWhen(false, nameof(Renamer))]
    public bool Preview { get; set; }

    /// <summary>
    /// The name of the renamer to use. If not provided, the default will be used.
    /// If <see cref="Preview"/> is set to true, this will be ignored.
    /// </summary>
    public RenamerInstance? Renamer { get; set; } = null;

    /// <summary>
    /// Do the rename operation.
    /// </summary>
    public bool Rename { get; set; } = true;

    /// <summary>
    /// Indicates whether empty directories should be deleted after
    /// relocating the file.
    /// </summary>
    public bool DeleteEmptyDirectories { get; set; } = true;

    /// <summary>
    /// Do the move operation.
    /// </summary>
    public bool Move { get; set; } = true;
}
