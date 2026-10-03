using System.Collections.Generic;
using Newtonsoft.Json;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Server.Models.Metadata.Embedded;

/// <summary>
///   What a source said of a character that needs no column of its own, kept
///   as one JSON object in <see cref="Metadata_Character.ExtraData"/>.
/// </summary>
/// <remarks>
///   A property missing from the stored JSON reads as its default, and one
///   the record does not know is skipped, so a new extra needs no schema
///   step. Keep the members values or strings, so two records still compare
///   by value.
/// </remarks>
public sealed record Metadata_CharacterExtra : IMetadataDefaultImages<Metadata_CharacterExtra>
{
    #region Fields

    /// <summary>
    ///   A record with no value set.
    /// </summary>
    private static readonly Metadata_CharacterExtra _empty = new();

    #endregion

    #region Properties

    /// <summary>
    ///   The resource ID of the primary image the character's source pins as its
    ///   default, or <c>null</c> for none.
    /// </summary>
    public string? PrimaryResourceID { get; init; }

    /// <summary>
    ///   Whether no value is set.
    /// </summary>
    [JsonIgnore]
    public bool IsEmpty => this == _empty;

    #endregion

    #region Methods

    /// <summary>
    ///   The record, or <c>null</c> when no value is set, which is what the
    ///   character stores then.
    /// </summary>
    /// <returns>The record, or <c>null</c>.</returns>
    public Metadata_CharacterExtra? NullIfEmpty()
        => IsEmpty ? null : this;

    /// <inheritdoc />
    public string? GetDefaultResourceID(ImageEntityType imageType)
        => imageType switch
        {
            ImageEntityType.Primary => PrimaryResourceID,
            _ => null,
        };

    /// <inheritdoc />
    public Metadata_CharacterExtra WithDefaultResourceIDs(IReadOnlyDictionary<ImageEntityType, string> resourceIDs)
        => this with
        {
            PrimaryResourceID = MetadataDefaultImages.Of(resourceIDs, ImageEntityType.Primary),
        };

    #endregion
}
