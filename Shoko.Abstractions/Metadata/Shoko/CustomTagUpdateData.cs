using System;

namespace Shoko.Abstractions.Metadata.Shoko;

/// <summary>
///   Data transfer object (DTO) for updating an existing custom tag.
///   Supports partial updates — only non-null fields are applied.
/// </summary>
public sealed class CustomTagUpdateData
{
    /// <summary>
    ///   The new name of the custom tag. Set to <c>null</c> to leave
    ///   unchanged.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    ///   The new overview of the custom tag. Set to <c>null</c> to
    ///   leave unchanged.
    /// </summary>
    public string? Overview { get; set; }

    /// <summary>
    ///   The new description of the custom tag.
    /// </summary>
    [Obsolete("Use Overview instead.")]
    public string? Description { get => Overview; set => Overview = value; }
}
