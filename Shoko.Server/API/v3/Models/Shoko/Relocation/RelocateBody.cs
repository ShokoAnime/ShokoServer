#nullable enable
using System.ComponentModel.DataAnnotations;

namespace Shoko.Server.API.v3.Models.Shoko.Relocation;

/// <summary>
/// Represents the information required to create or move to a new file
/// location.
/// </summary>
public class RelocateBody
{
    /// <summary>
    /// The id of the <see cref="ManagedFolder"/> where this file should
    /// be relocated to.
    /// </summary>
    [Required]
    public int ManagedFolderID { get; set; }

    /// <summary>
    /// The new relative path from the <see cref="ManagedFolder"/>'s path
    /// on the server.
    /// </summary>
    [Required]
    public string RelativePath { get; set; } = string.Empty;

    /// <summary>
    /// Indicates whether empty directories should be deleted after
    /// relocating the file.
    /// </summary>
    public bool DeleteEmptyDirectories { get; set; } = true;
}
