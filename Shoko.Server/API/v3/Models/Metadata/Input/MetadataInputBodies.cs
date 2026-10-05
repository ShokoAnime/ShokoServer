using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.CrossReferences;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Providers;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v3.Models.Common;

namespace Shoko.Server.API.v3.Models.Metadata.Input;

/// <summary>
/// Asks for several entries of one kind at once.
/// </summary>
public class MetadataBulkFetchBody
{
    /// <summary>
    /// The entries: the source's own IDs, or full identifiers such as
    /// <c>anilist://series/21</c>, which need no escaping. Unknown entries are
    /// left out.
    /// </summary>
    [Required, MaxLength(1000)]
    public List<string> IDs { get; set; } = [];

    /// <summary>
    /// The extra details to send each entry with.
    /// </summary>
    public HashSet<MetadataIncludeDetails>? Include { get; set; }
}

/// <summary>
/// How to refresh an entry.
/// </summary>
public class MetadataRefreshBody
{
    /// <summary>
    /// Refresh the entry however recently it last was.
    /// </summary>
    public bool Force { get; set; }

    /// <summary>
    /// Also download the images.
    /// </summary>
    [DefaultValue(true)]
    public bool DownloadImages { get; set; } = true;

    /// <summary>
    /// Also fetch the alternate orderings, or <c>null</c> to go by the
    /// settings.
    /// </summary>
    public bool? DownloadAlternateOrdering { get; set; }

    /// <summary>
    /// Run the refresh now and wait for it, rather than queueing it.
    /// </summary>
    public bool Immediate { get; set; }

    /// <summary>
    /// Do nothing when the entry is stored already.
    /// </summary>
    public bool SkipIfExists { get; set; }

    /// <summary>
    /// Fetch only what is cheap, for a caller waiting on the result. Only
    /// used together with <see cref="Immediate"/>.
    /// </summary>
    public bool QuickRefresh { get; set; }

    /// <summary>
    /// The refresh options the body asks for.
    /// </summary>
    /// <param name="quick">Whether it is a quick refresh.</param>
    /// <returns>The options.</returns>
    public MetadataRefreshOptions ToOptions(bool quick)
        => new()
        {
            QuickRefresh = quick,
            DownloadImages = DownloadImages,
            DownloadAlternateOrdering = DownloadAlternateOrdering,
            Reason = MetadataRefreshReason.Requested,
        };
}

/// <summary>
/// How to refresh a creator, character, studio or network.
/// </summary>
public class MetadataEntityRefreshBody
{
    /// <summary>
    /// Refresh it however recently it was refreshed.
    /// </summary>
    public bool Force { get; set; }

    /// <summary>
    /// Run the refresh now and wait for it, rather than queueing it.
    /// </summary>
    public bool Immediate { get; set; }
}

/// <summary>
/// How to download an entry's images.
/// </summary>
public class MetadataDownloadImagesBody
{
    /// <summary>
    /// Download the wanted images again even when they are there.
    /// </summary>
    public bool Force { get; set; }

    /// <summary>
    /// Run the download now and wait for it, rather than queueing it.
    /// </summary>
    public bool Immediate { get; set; }
}

/// <summary>
/// Changes how a metadata provider is set up. Left-out fields are kept.
/// </summary>
public class MetadataProviderUpdateBody
{
    /// <summary>
    /// The kinds of entries to turn the provider on for; every other kind is
    /// turned off. An empty set turns it off. Each kind must be one the
    /// provider can answer for.
    /// </summary>
    [RegisteredMetadataValues]
    public HashSet<MetadataEntityType>? EnabledEntityTypes { get; set; }

    /// <summary>
    /// Make this the provider that works out what an anime is for its source,
    /// or, when it is and this is <c>false</c>, leave the source without one.
    /// </summary>
    public bool? IsAutoLinker { get; set; }

    /// <summary>
    /// Whether the source links new anime on its own.
    /// </summary>
    public bool? AutoLink { get; set; }

    /// <summary>
    /// Whether the source's automatic links may point at restricted entries.
    /// </summary>
    public bool? AutoLinkRestricted { get; set; }
}

/// <summary>
/// Sets the order of the providers claiming one kind of entry on a source,
/// and which of them are enabled.
/// </summary>
public class MetadataProviderOrderBody
{
    /// <summary>
    /// The kind of entry.
    /// </summary>
    [Required, RegisteredMetadataValues]
    public MetadataEntityType EntityType { get; set; } = null!;

    /// <summary>
    /// The providers, tried by <see cref="MetadataProviderOrderEntryBody.Priority"/>.
    /// Each must claim the kind on the source. Those left out keep their
    /// place and switch after them.
    /// </summary>
    [Required]
    public List<MetadataProviderOrderEntryBody> Providers { get; set; } = [];
}

/// <summary>
/// One provider's place in a <see cref="MetadataProviderOrderBody"/>.
/// </summary>
public class MetadataProviderOrderEntryBody
{
    /// <summary>
    /// The provider's ID.
    /// </summary>
    [Required]
    public Guid ProviderID { get; set; }

    /// <summary>
    /// Whether the provider may answer. Defaults to <c>true</c>.
    /// </summary>
    [DefaultValue(true)]
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Where the provider goes, lowest first; ties keep the order given.
    /// </summary>
    public int Priority { get; set; }
}

/// <summary>
/// Asks for several series or movies of a source at once, stored copies
/// first and the rest looked up.
/// </summary>
public class MetadataBulkLookupBody
{
    /// <summary>
    /// The entries: the source's own IDs, or full identifiers such as
    /// <c>anilist://series/21</c>.
    /// </summary>
    [Required, MaxLength(100)]
    public List<string> IDs { get; set; } = [];
}

/// <summary>
/// Which of a source's links to export.
/// </summary>
public class MetadataExportBody
{
    /// <summary>
    /// The sections to write, or <c>null</c> or empty for all of them.
    /// </summary>
    public HashSet<MetadataCrossReferenceSection>? Sections { get; set; }

    /// <summary>
    /// Only the links of this AniDB anime.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int? AnidbAnimeID { get; set; }

    /// <summary>
    /// Only the links of this AniDB episode.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int? AnidbEpisodeID { get; set; }

    /// <summary>
    /// Only the links to or into this series, by the source's own ID.
    /// </summary>
    public string? SeriesID { get; set; }

    /// <summary>
    /// Only the links to this episode, by the source's own ID.
    /// </summary>
    public string? EpisodeID { get; set; }

    /// <summary>
    /// Only the links to this movie, by the source's own ID.
    /// </summary>
    public string? MovieID { get; set; }

    /// <summary>
    /// Whether to write the links made automatically: <c>True</c> for all
    /// links, <c>Only</c> for only the automatic ones, <c>False</c> for only
    /// the ones a person made.
    /// </summary>
    [DefaultValue(IncludeOnlyFilter.True)]
    public IncludeOnlyFilter Automatic { get; set; } = IncludeOnlyFilter.True;

    /// <summary>
    /// Whether to write series links with episode links under them and
    /// episode links to an episode: <c>True</c> for all, <c>Only</c> for only
    /// those, <c>False</c> for only the others.
    /// </summary>
    [DefaultValue(IncludeOnlyFilter.True)]
    public IncludeOnlyFilter WithEpisodes { get; set; } = IncludeOnlyFilter.True;

    /// <summary>
    /// Write a comment naming what each link joins.
    /// </summary>
    public bool IncludeComments { get; set; }

    /// <summary>
    /// The export options the body asks for.
    /// </summary>
    /// <returns>The options.</returns>
    public MetadataCrossReferenceExportOptions ToOptions()
        => new()
        {
            Sections = Sections is { Count: > 0 }
                ? Sections.Aggregate(MetadataCrossReferenceSections.None, (sections, section) => sections | section switch
                {
                    MetadataCrossReferenceSection.Movie => MetadataCrossReferenceSections.Movie,
                    MetadataCrossReferenceSection.Series => MetadataCrossReferenceSections.Series,
                    MetadataCrossReferenceSection.Episode => MetadataCrossReferenceSections.Episode,
                    _ => MetadataCrossReferenceSections.None,
                })
                : MetadataCrossReferenceSections.All,
            AnidbAnimeID = AnidbAnimeID,
            AnidbEpisodeID = AnidbEpisodeID,
            ProviderSeriesID = string.IsNullOrEmpty(SeriesID) ? null : SeriesID,
            ProviderEpisodeID = string.IsNullOrEmpty(EpisodeID) ? null : EpisodeID,
            ProviderMovieID = string.IsNullOrEmpty(MovieID) ? null : MovieID,
            Automatic = ToFilter(Automatic),
            WithEpisodes = ToFilter(WithEpisodes),
            IncludeComments = IncludeComments,
        };

    /// <summary>
    /// A three-way filter as the export options take it.
    /// </summary>
    /// <param name="filter">The filter.</param>
    /// <returns><c>true</c> for only, <c>false</c> for none, <c>null</c> for all.</returns>
    private static bool? ToFilter(IncludeOnlyFilter filter)
        => filter switch
        {
            IncludeOnlyFilter.Only => true,
            IncludeOnlyFilter.False => false,
            _ => null,
        };
}

/// <summary>
/// A section of a cross-reference file.
/// </summary>
[JsonConverter(typeof(StringEnumConverter))]
public enum MetadataCrossReferenceSection
{
    /// <summary>
    /// The movie links, one per AniDB episode standing for a movie.
    /// </summary>
    Movie,

    /// <summary>
    /// The series links, one per AniDB anime and series.
    /// </summary>
    Series,

    /// <summary>
    /// The episode links, one per AniDB episode and episode.
    /// </summary>
    Episode,
}

/// <summary>
/// Links a Shoko series to a series of a source.
/// </summary>
public class MetadataLinkSeriesBody
{
    /// <summary>
    /// The source's ID for the series, or its full identifier.
    /// </summary>
    [Required]
    public string ID { get; set; } = string.Empty;

    /// <summary>
    /// Replace the series' other links to the source rather than adding this
    /// one beside them.
    /// </summary>
    public bool Replace { get; set; }

    /// <summary>
    /// Refresh the linked series even when it is stored and was refreshed
    /// before. Otherwise only a series never refreshed in full is.
    /// </summary>
    public bool Refresh { get; set; }
}

/// <summary>
/// Unlinks a Shoko series or episode from one entry of a source, or from
/// every one.
/// </summary>
public class MetadataUnlinkBody
{
    /// <summary>
    /// The source's ID for the entry to unlink, or its full identifier;
    /// every linked entry of the kind when left out.
    /// </summary>
    public string? ID { get; set; }

    /// <summary>
    /// Also purge what was unlinked, when nothing else links to it.
    /// </summary>
    public bool Purge { get; set; }
}

/// <summary>
/// Links an AniDB episode of a Shoko series to a movie of a source.
/// </summary>
public class MetadataLinkMovieBody
{
    /// <summary>
    /// The source's ID for the movie, or its full identifier.
    /// </summary>
    [Required]
    public string ID { get; set; } = string.Empty;

    /// <summary>
    /// The AniDB episode standing for the movie. Left out on a series' route,
    /// the series' first regular episode.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int? EpisodeID { get; set; }

    /// <summary>
    /// Replace the episode's other movie links to the source rather than
    /// adding this one beside them.
    /// </summary>
    public bool Replace { get; set; }

    /// <summary>
    /// Refresh the linked movie even when it is stored and was refreshed
    /// before. Otherwise only a movie never refreshed in full is.
    /// </summary>
    public bool Refresh { get; set; }
}

/// <summary>
/// Unlinks one movie of a source, or every one, from a Shoko series' AniDB
/// episodes.
/// </summary>
public class MetadataUnlinkMovieBody
{
    /// <summary>
    /// The source's ID for the movie, or its full identifier; every linked
    /// movie when left out.
    /// </summary>
    public string? ID { get; set; }

    /// <summary>
    /// Only the links of this AniDB episode; every episode of the series when
    /// left out.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int? EpisodeID { get; set; }

    /// <summary>
    /// Also purge what was unlinked, when nothing else links to it.
    /// </summary>
    public bool Purge { get; set; }
}

/// <summary>
/// Changes a Shoko series' episode links to a source by hand.
/// </summary>
public class MetadataOverrideEpisodeLinksBody
{
    /// <summary>
    /// Clear every episode link first, marking each episode as linked to
    /// nothing, so only the links given here are left.
    /// </summary>
    public bool UnsetAll { get; set; }

    /// <summary>
    /// The links to set.
    /// </summary>
    [Required]
    public List<MetadataOverrideEpisodeLinkBody> Mapping { get; set; } = [];
}

/// <summary>
/// One episode link to set by hand.
/// </summary>
public class MetadataOverrideEpisodeLinkBody
{
    /// <summary>
    /// The AniDB episode.
    /// </summary>
    [Required, Range(1, int.MaxValue)]
    public int AniDBID { get; set; }

    /// <summary>
    /// The source's ID for the episode, or its full identifier. Left out,
    /// empty or <c>0</c> links the AniDB episode to nothing, which replaces
    /// its other links.
    /// </summary>
    public string? ID { get; set; }

    /// <summary>
    /// Replace the AniDB episode's other links rather than adding this one
    /// beside them.
    /// </summary>
    public bool Replace { get; set; }

    /// <summary>
    /// Where to put the link among the AniDB episode's links, from <c>0</c>,
    /// when it is added beside them.
    /// </summary>
    public int? Index { get; set; }

    /// <summary>
    /// Whether the body links the AniDB episode to nothing.
    /// </summary>
    [JsonIgnore]
    public bool IsEmpty
        => string.IsNullOrWhiteSpace(ID) || ID is "0";
}

/// <summary>
/// Matches a Shoko series' episodes against a series of a source.
/// </summary>
public class MetadataAutoMatchEpisodesBody
{
    /// <summary>
    /// The source's ID for the series to match against, or its full
    /// identifier; the first linked series when left out. A series not linked
    /// yet is linked first.
    /// </summary>
    public string? ParentID { get; set; }

    /// <summary>
    /// Only match against this season of the series, by the source's ID or
    /// its full identifier.
    /// </summary>
    public string? SeasonID { get; set; }

    /// <summary>
    /// Keep the links already there, only filling in the episodes without
    /// one.
    /// </summary>
    [DefaultValue(true)]
    public bool KeepExisting { get; set; } = true;

    /// <summary>
    /// Leave out the episodes other anime claim; left out, the settings
    /// decide.
    /// </summary>
    public bool? ConsiderExistingOtherLinks { get; set; }
}

/// <summary>
/// Whether a source may link a Shoko series on its own.
/// </summary>
public class MetadataAutoLinkingBody
{
    /// <summary>
    /// Keep the source from linking the series on its own.
    /// </summary>
    [Required]
    public bool Disabled { get; set; }
}

/// <summary>
/// Sets how far the links to one entry of a source are trusted, without
/// removing or matching anything.
/// </summary>
public class MetadataSetMatchRatingBody
{
    /// <summary>
    /// The rating to give the links; verified by a user when left out.
    /// </summary>
    [DefaultValue(MatchRating.UserVerified)]
    public MatchRating MatchRating { get; set; } = MatchRating.UserVerified;

    /// <summary>
    /// Only the links from this AniDB anime; every link to the entry when
    /// left out.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int? AnidbAnimeID { get; set; }

    /// <summary>
    /// Only the links from this AniDB episode, which series-level links never
    /// match; every link to the entry when left out.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int? AnidbEpisodeID { get; set; }
}

/// <summary>
/// Sets how far several links to a source are trusted at once, without
/// removing or matching anything.
/// </summary>
public class MetadataBulkSetMatchRatingBody
{
    /// <summary>
    /// The rating to give the links; verified by a user when left out.
    /// </summary>
    [DefaultValue(MatchRating.UserVerified)]
    public MatchRating MatchRating { get; set; } = MatchRating.UserVerified;

    /// <summary>
    /// The links, as the cross-reference routes send them. Every one must be
    /// stored, or nothing changes.
    /// </summary>
    [Required, MinLength(1), MaxLength(1000)]
    public List<MetadataCrossReferenceKeyBody> CrossReferences { get; set; } = [];
}

/// <summary>
/// Names one stored link to a source, by the fields the cross-reference
/// routes send.
/// </summary>
public class MetadataCrossReferenceKeyBody
{
    /// <summary>
    /// The level the link is made at: <c>series</c>, <c>movie</c> or
    /// <c>episode</c>. A film claiming a whole anime is a <c>series</c> link.
    /// </summary>
    [Required]
    public MetadataEntityType EntityType { get; set; } = null!;

    /// <summary>
    /// The AniDB anime the link is made from.
    /// </summary>
    [Required, Range(1, int.MaxValue)]
    public int AnidbAnimeID { get; set; }

    /// <summary>
    /// The AniDB episode a movie or episode link is made from.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int? AnidbEpisodeID { get; set; }

    /// <summary>
    /// The source's ID of the linked entry, or its full identifier; left out
    /// for an episode deliberately linked to nothing.
    /// </summary>
    public string? ID { get; set; }
}

/// <summary>
/// Changes how an image contributor is set up.
/// </summary>
public class MetadataImageContributorUpdateBody
{
    /// <summary>
    /// The sources and kinds to turn the contributor on for; every other one
    /// it can add images for is turned off, and the images it added there are
    /// removed. An empty list turns it off. Each pair must be one the
    /// contributor can add images for.
    /// </summary>
    [Required]
    public List<MetadataEntityScopeEntry> Enabled { get; set; } = [];
}
