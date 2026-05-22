namespace Shoko.Abstractions.Metadata.Shoko;

/// <summary>
///   Data transfer object (DTO) for creating a new custom tag.
/// </summary>
public sealed class CustomTagData
{
    /// <summary>
    ///   The name of the custom tag.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    ///   The description of the custom tag.
    /// </summary>
    public string? Description { get; set; }
}
