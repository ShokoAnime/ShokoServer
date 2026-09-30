using System;
using System.Collections.Generic;
using Shoko.Abstractions.Filtering;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Tests;

public class TestFilterable : IFilterableInfo
{
    public string Name { get; init; } = null!;
    public string MainName { get; init; } = null!;
    public string OriginalName { get; init; } = null!;
    public string SortName { get; init; } = null!;
    public IReadOnlySet<string> Names { get; init; } = null!;
    public IReadOnlySet<string> PreferredNames { get; init; } = null!;
    public string Description { get; init; } = null!;
    public IReadOnlySet<string> Descriptions { get; init; } = null!;
    public IReadOnlySet<string> SeriesIDs { get; init; } = null!;
    public int GroupID { get; init; }
    public int TopLevelGroupID { get; init; }
    public IReadOnlySet<string> GroupIDs { get; init; } = null!;
    public IReadOnlySet<string> AnidbAnimeIDs { get; init; } = null!;
    public int SeriesCount { get; init; }
    public int GroupCount { get; init; }
    public int TotalGroupCount { get; init; }
    public int MissingEpisodes { get; init; }
    public int MissingEpisodesCollecting { get; init; }
    public int VideoFiles { get; init; }
    public IReadOnlySet<string> AnidbTagIDs { get; init; } = null!;
    public IReadOnlySet<string> AnidbTags { get; init; } = null!;
    public IReadOnlySet<string> CustomTagIDs { get; init; } = null!;
    public IReadOnlySet<string> CustomTags { get; init; } = null!;
    public IReadOnlySet<int> Years { get; init; } = null!;
    public IReadOnlySet<(int year, YearlySeason season)> Seasons { get; init; } = null!;
    public IReadOnlySet<ImageEntityType> AvailableImageTypes { get; init; } = null!;
    public IReadOnlySet<ImageEntityType> PreferredImageTypes { get; init; } = null!;
    public int AnidbSuggestions { get; init; }
    public int TmdbSuggestions { get; init; }
    /// <summary>
    /// The suggestions every source but AniDB and TMDB makes, since the double
    /// keeps one count for all of them.
    /// </summary>
    public int OtherSuggestions { get; init; }
    public IReadOnlySet<MetadataSource> LinkedSources { get; init; } = new HashSet<MetadataSource>();
    public IReadOnlySet<MetadataSource> UnlinkedSources { get; init; } = new HashSet<MetadataSource>();
    public IReadOnlySet<MetadataSource> AutoLinkingDisabledSources { get; init; } = new HashSet<MetadataSource>();
    public IReadOnlyDictionary<MetadataSource, int> AutomaticEpisodeLinks { get; init; } = new Dictionary<MetadataSource, int>();
    public IReadOnlyDictionary<MetadataSource, int> UserVerifiedEpisodeLinks { get; init; } = new Dictionary<MetadataSource, int>();
    public IReadOnlyDictionary<MetadataSource, int> MissingEpisodeLinks { get; init; } = new Dictionary<MetadataSource, int>();
    public IReadOnlyDictionary<MetadataSource, int> AutomaticLinks { get; init; } = new Dictionary<MetadataSource, int>();
    public IReadOnlyDictionary<MetadataSource, int> UserVerifiedLinks { get; init; } = new Dictionary<MetadataSource, int>();
    public IReadOnlyDictionary<MetadataSource, IReadOnlySet<string>> Genres { get; init; } = new Dictionary<MetadataSource, IReadOnlySet<string>>();
    public IReadOnlyDictionary<MetadataSource, IReadOnlySet<string>> SourceTags { get; init; } = new Dictionary<MetadataSource, IReadOnlySet<string>>();
    /// <summary>
    /// The genres and tags narrowed to one kind of linked entry, by source and
    /// entity type. The unnarrowed maps above answer when no entity type is asked for.
    /// </summary>
    public IReadOnlyDictionary<(MetadataSource, MetadataEntityType), IReadOnlySet<string>> NarrowedGenres { get; init; } = new Dictionary<(MetadataSource, MetadataEntityType), IReadOnlySet<string>>();
    public IReadOnlyDictionary<(MetadataSource, MetadataEntityType), IReadOnlySet<string>> NarrowedTags { get; init; } = new Dictionary<(MetadataSource, MetadataEntityType), IReadOnlySet<string>>();
    public int GetAutomaticEpisodeLinks(MetadataSource source) => AutomaticEpisodeLinks.GetValueOrDefault(source);
    public int GetUserVerifiedEpisodeLinks(MetadataSource source) => UserVerifiedEpisodeLinks.GetValueOrDefault(source);
    public int GetMissingEpisodeLinks(MetadataSource source) => MissingEpisodeLinks.GetValueOrDefault(source);
    public int GetAutomaticLinks(MetadataSource source) => AutomaticLinks.GetValueOrDefault(source);
    public int GetUserVerifiedLinks(MetadataSource source) => UserVerifiedLinks.GetValueOrDefault(source);
    public IReadOnlySet<string> GetGenres(MetadataSource source, MetadataEntityType? entityType = null)
        => (entityType is { } type ? NarrowedGenres.GetValueOrDefault((source, type)) : Genres.GetValueOrDefault(source)) ?? new HashSet<string>();
    public IReadOnlySet<string> GetTags(MetadataSource source, MetadataEntityType? entityType = null)
        => (entityType is { } type ? NarrowedTags.GetValueOrDefault((source, type)) : SourceTags.GetValueOrDefault(source)) ?? new HashSet<string>();
    public int GetSuggestions(MetadataSource source) => source switch
    {
        _ when source == MetadataSource.AniDB => AnidbSuggestions,
        _ when source == MetadataSource.TMDB => TmdbSuggestions,
        _ => OtherSuggestions,
    };
    public int TotalSuggestions { get; init; }
    public int LocalSuggestions { get; init; }
    public bool HasTraktLink { get; init; }
    public bool HasTraktAutoLinkingDisabled { get; init; }
    public bool HasMissingTraktLink { get; init; }
    public bool IsFinished { get; init; }
    public bool IsRestricted { get; init; }
    public PartialDateOnly? AirDate { get; init; }
    public PartialDateOnly? LastAirDate { get; init; }
    public DateTime AddedDate { get; init; }
    public DateTime? LastAddedDate { get; init; }
    public int EpisodeCount { get; init; }
    public int TotalEpisodeCount { get; init; }
    public int HiddenEpisodes { get; init; }
    public EpisodeCounts EpisodeCounts { get; init; } = null!;
    public EpisodeCounts LocalEpisodeCounts { get; init; } = null!;
    public EpisodeCounts MissingEpisodeCounts { get; init; } = null!;
    public EpisodeCounts UnairedEpisodeCounts { get; init; } = null!;
    public FileSourceCounts FileSourceCounts { get; init; } = null!;
    public IReadOnlyDictionary<string, int> ReleaseProviderCounts { get; init; } = null!;
    public double LowestAniDBRating { get; init; }
    public double AverageAniDBRating { get; init; }
    public double HighestAniDBRating { get; init; }
    public IReadOnlySet<string> VideoSources { get; init; } = null!;
    public IReadOnlySet<string> SharedVideoSources { get; init; } = null!;
    public IReadOnlySet<AnimeType> AnimeTypes { get; init; } = null!;
    public IReadOnlySet<string> AudioLanguages { get; init; } = null!;
    public IReadOnlySet<string> SharedAudioLanguages { get; init; } = null!;
    public IReadOnlySet<string> SubtitleLanguages { get; init; } = null!;
    public IReadOnlySet<string> SharedSubtitleLanguages { get; init; } = null!;
    public IReadOnlySet<string> Resolutions { get; init; } = null!;
    public IReadOnlySet<string> ManagedFolderIDs { get; init; } = null!;
    public IReadOnlySet<string> ManagedFolderNames { get; init; } = null!;
    public IReadOnlySet<string> FilePaths { get; init; } = null!;
    public IReadOnlySet<string> AbsoluteFilePaths { get; init; } = null!;
    public IReadOnlySet<string> ContainingFolderPaths { get; init; } = null!;
    public IReadOnlySet<string> CharacterIDs { get; init; } = null!;
    public IReadOnlyDictionary<CastRoleType, IReadOnlySet<string>> CharacterAppearances { get; init; } = null!;
    public IReadOnlySet<string> CreatorIDs { get; init; } = null!;
    public IReadOnlyDictionary<CrewRoleType, IReadOnlySet<string>> CreatorRoles { get; init; } = null!;
    public IReadOnlySet<string> ReleaseGroupNames { get; init; } = null!;
    public IReadOnlySet<string> ReleaseProviderNames { get; init; } = null!;
}
