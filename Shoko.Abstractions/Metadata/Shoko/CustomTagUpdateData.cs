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
    ///   The new description of the custom tag. Set to <c>null</c> to
    ///   leave unchanged.
    /// </summary>
    public string? Description { get; set; }
}
