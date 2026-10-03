using System.Collections.Generic;
using Newtonsoft.Json;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Server.Models.Metadata.Embedded;

/// <summary>
///   What a source said of an episode that needs no column of its own, kept
///   as one JSON object in <see cref="Metadata_Episode.ExtraData"/>.
/// </summary>
/// <remarks>
///   A property missing from the stored JSON reads as its default, and one
///   the record does not know is skipped, so a new extra needs no schema
///   step. Keep the members values or strings, so two records still compare
///   by value.
/// </remarks>
public sealed record Metadata_EpisodeExtra : IMetadataDefaultImages<Metadata_EpisodeExtra>
{
    #region Fields

    /// <summary>
    ///   A record with no value set.
    /// </summary>
    private static readonly Metadata_EpisodeExtra _empty = new();

    #endregion

    #region Properties

    /// <summary>
    ///   The season of the regular episode a special airs before, as its
    ///   source said. Only kept in season 0.
    /// </summary>
    public int? AirsBeforeSeasonNumber { get; init; }

    /// <summary>
    ///   The number of the regular episode a special airs before, as its
    ///   source said. Only kept in season 0.
    /// </summary>
    public int? AirsBeforeEpisodeNumber { get; init; }

    /// <summary>
    ///   The season a special airs after, as its source said. Only kept in
    ///   season 0.
    /// </summary>
    public int? AirsAfterSeasonNumber { get; init; }

    /// <summary>
    ///   The resource ID of the backdrop the episode's source pins as its
    ///   default, or <c>null</c> for none.
    /// </summary>
    public string? BackdropResourceID { get; init; }

    /// <summary>
    ///   Whether no value is set.
    /// </summary>
    [JsonIgnore]
    public bool IsEmpty => this == _empty;

    #endregion

    #region Methods

    /// <summary>
    ///   The record, or <c>null</c> when no value is set, which is what the
    ///   episode stores then.
    /// </summary>
    /// <returns>The record, or <c>null</c>.</returns>
    public Metadata_EpisodeExtra? NullIfEmpty()
        => IsEmpty ? null : this;

    /// <inheritdoc />
    public string? GetDefaultResourceID(ImageEntityType imageType)
        => imageType switch
        {
            ImageEntityType.Backdrop => BackdropResourceID,
            _ => null,
        };

    /// <inheritdoc />
    public Metadata_EpisodeExtra WithDefaultResourceIDs(IReadOnlyDictionary<ImageEntityType, string> resourceIDs)
        => this with
        {
            BackdropResourceID = MetadataDefaultImages.Of(resourceIDs, ImageEntityType.Backdrop),
        };

    #endregion
}
