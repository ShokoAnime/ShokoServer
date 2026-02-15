
using System;
using Shoko.Abstractions.Enums;
using Shoko.Server.Server;

#nullable enable
namespace Shoko.Server.Models.Interfaces;

public interface IEntityMetadata
{
    /// <summary>
    /// Entity Id.
    /// </summary>
    public int Id { get; }

    /// <summary>
    /// Entity type.
    /// </summary>
    public ForeignEntityType Type { get; }

    /// <summary>
    /// Entity data source.
    /// </summary>
    public DataSource DataSource { get; }

    /// <summary>
    /// The english title of the movie, used as a fallback for when no title
    /// is available in the preferred language.
    /// </summary>
    public string? EnglishTitle { get; }

    /// <summary>
    /// The english overview, used as a fallback for when no overview is
    /// available in the preferred language.
    /// </summary>
    public string? EnglishOverview { get; }

    /// <summary>
    /// Original title in the original language.
    /// </summary>
    public string? OriginalTitle { get; }

    /// <summary>
    /// The original language this show was shot in, just as a title language
    /// enum instead.
    /// </summary>
    public TitleLanguage? OriginalLanguage { get; }

    /// <summary>
    /// The original language this show was shot in.
    /// </summary>
    public string? OriginalLanguageCode { get; }

    /// <summary>
    /// When the entity was first released, if applicable and known.
    /// </summary>
    public DateOnly? ReleasedAt { get; }

    /// <summary>
    /// When the metadata was first downloaded.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When the metadata was last synchronized with the remote.
    /// </summary>
    public DateTime LastUpdatedAt { get; set; }
}
