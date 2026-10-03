using System;
using System.Collections.Generic;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.CrossReferences;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.Video;
using Shoko.Tests.Infrastructure;

namespace Shoko.Tests.API.Metadata;

/// <summary>
/// Plain entries of a fake plugin source, with every list empty unless a test
/// fills it, for the generic metadata routes to read.
/// </summary>
public static class FakeMetadataEntries
{
    #region Identifiers

    /// <summary>
    /// The fake plugin source.
    /// </summary>
    public static MetadataSource Source => TestSources.Plugin;

    /// <summary>
    /// An identifier on the fake source.
    /// </summary>
    /// <param name="entityType">The kind of entry.</param>
    /// <param name="id">The source's ID.</param>
    /// <returns>The identifier.</returns>
    public static MetadataGuid ID(MetadataEntityType entityType, string id)
        => new(Source, entityType, id);

    #endregion

    #region Text

    public sealed class FakeTitle(string value, TitleLanguage language = TitleLanguage.English, TitleType type = TitleType.Official, MetadataSource? source = null) : ITitle
    {
        public TitleLanguage Language { get; } = language;

        public string LanguageCode => Language is TitleLanguage.Japanese ? "ja" : "en";

        public string? CountryCode => null;

        public string Value { get; } = value;

        public MetadataSource Source { get; } = source ?? FakeMetadataEntries.Source;

        public TitleType Type { get; } = type;

        public bool IsSynthesized { get; init; }

        public int? ID { get; init; }

        public MetadataGuid? EntityID { get; init; }

        public int? ReferenceID { get; init; }

        public bool IsEnabled { get; init; } = true;

        public TextPreference Preference { get; init; }

        public int Ordering { get; init; }

        public string? ScriptCode { get; init; }

        public bool IsInlineDefault { get; init; }

        public bool Equals(IText? other)
            => IText.Equals(this, other);

        public bool Equals(ITitle? other)
            => ITitle.Equals(this, other);
    }

    #endregion

    #region Entries

    public sealed class FakeSeries(string id, string title) : ISeries
    {
        public MetadataGuid ID { get; } = FakeMetadataEntries.ID(MetadataEntityType.Series, id);

        public string Title { get; set; } = title;

        public ITitle DefaultTitle => new FakeTitle(Title);

        public ITitle? PreferredTitle => DefaultTitle;

        public IReadOnlyList<ITitle> Titles { get; set; } = [new FakeTitle(title)];

        public IText? DefaultOverview => null;

        public IText? PreferredOverview => null;

        public IReadOnlyList<IText> Overviews { get; set; } = [];

        public IReadOnlyList<ICast> Cast { get; set; } = [];

        public IReadOnlyList<ICrew> Crew { get; set; } = [];

        public IReadOnlyList<IStudio> Studios { get; set; } = [];

        public IReadOnlyList<IContentRating> ContentRatings { get; set; } = [];

        public IReadOnlyList<(int Year, YearlySeason Season)> YearlySeasons { get; set; } = [];

        public IReadOnlyList<Resource> Resources { get; set; } = [];

        public IReadOnlyList<MetadataGuid> CrossSourceIDs { get; set; } = [];

        public IReadOnlyList<ITag> Tags { get; set; } = [];

        public IReadOnlyList<int> ShokoSeriesIDs { get; set; } = [];

        public AnimeType Type { get; set; } = AnimeType.TV;

        public PartialDateOnly? AirDate { get; set; }

        public PartialDateOnly? EndDate { get; set; }

        public double Rating { get; set; }

        public int RatingVotes { get; set; }

        public bool Restricted { get; set; }

        public ReleaseStatus ReleaseStatus { get; set; }

        public SourceMaterial SourceMaterial { get; set; }

        public string? OriginalLanguageCode { get; set; }

        public double? Popularity { get; set; }

        public int? FavoriteCount { get; set; }

        public IReadOnlyList<INetwork> Networks { get; set; } = [];

        public IReadOnlyList<string> ProductionCountries { get; set; } = [];

        public IReadOnlyList<IShokoSeries> ShokoSeries { get; set; } = [];

        public IReadOnlyList<IRelatedMetadata<ISeries, ISeries>> RelatedSeries { get; set; } = [];

        public IReadOnlyList<IRelatedMetadata<ISeries, IMovie>> RelatedMovies { get; set; } = [];

        public IReadOnlyList<ISuggestedMetadata<ISeries, ISeries>> Suggestions { get; set; } = [];

        public IReadOnlyList<ISuggestedMetadata<ISeries, ISeries>> SuggestedBy { get; set; } = [];

        public IReadOnlyList<IVideoCrossReference> VideoCrossReferences { get; set; } = [];

        public IReadOnlyList<IMetadataSeriesCrossReference> MetadataSeriesCrossReferences { get; set; } = [];

        public IReadOnlyList<IMetadataEpisodeCrossReference> MetadataEpisodeCrossReferences { get; set; } = [];

        public IReadOnlyList<IMetadataSeasonCrossReference> MetadataSeasonCrossReferences { get; set; } = [];

        public IReadOnlyList<IMetadataMovieCrossReference> MetadataMovieCrossReferences { get; set; } = [];

        public IReadOnlyList<ISeason> Seasons { get; set; } = [];

        public IReadOnlyList<IOrdering> Orderings { get; set; } = [];

        public IOrdering PreferredOrdering => null!;

        public IReadOnlyList<IEpisode> Episodes { get; set; } = [];

        public IReadOnlyList<IVideo> Videos { get; set; } = [];

        public EpisodeCounts EpisodeCounts { get; set; } = new();

        public DateTime CreatedAt { get; set; } = DateTime.UnixEpoch;

        public DateTime LastUpdatedAt { get; set; } = DateTime.UnixEpoch;

        public DateTime? LastRefreshedAt { get; set; }
    }

    public sealed class FakeEpisode(string id, string seriesID, int number, EpisodeType type = EpisodeType.Episode, int? seasonNumber = 1) : IEpisode
    {
        public MetadataGuid ID { get; } = FakeMetadataEntries.ID(MetadataEntityType.Episode, id);

        public string Title => $"Episode {EpisodeNumber}";

        public ITitle DefaultTitle => new FakeTitle(Title);

        public ITitle? PreferredTitle => DefaultTitle;

        public IReadOnlyList<ITitle> Titles { get; set; } = [];

        public IText? DefaultOverview => null;

        public IText? PreferredOverview => null;

        public IReadOnlyList<IText> Overviews { get; set; } = [];

        public IReadOnlyList<ICast> Cast { get; set; } = [];

        public IReadOnlyList<ICrew> Crew { get; set; } = [];

        public IReadOnlyList<Resource> Resources { get; set; } = [];

        public IReadOnlyList<MetadataGuid> CrossSourceIDs { get; set; } = [];

        public MetadataGuid SeriesID { get; } = FakeMetadataEntries.ID(MetadataEntityType.Series, seriesID);

        public MetadataGuid? SeasonID { get; set; }

        public IReadOnlyList<int> ShokoEpisodeIDs { get; set; } = [];

        public EpisodeType Type { get; } = type;

        public int EpisodeNumber { get; } = number;

        public int? SeasonNumber { get; } = seasonNumber;

        public double Rating { get; set; }

        public int RatingVotes { get; set; }

        public TimeSpan Runtime { get; set; } = TimeSpan.FromMinutes(24);

        public bool IsHidden { get; set; }

        public DateOnly? AirDate { get; set; }

        public DateTime? AirDateWithTime { get; set; }

        public ISeries Series { get; set; } = null!;

        public IReadOnlyList<IEpisodeOrderingInformation> Orderings { get; set; } = [];

        public IEpisodeOrderingInformation? PreferredOrdering => null;

        public IReadOnlyList<IShokoEpisode> ShokoEpisodes { get; set; } = [];

        public IReadOnlyList<IVideoCrossReference> VideoCrossReferences { get; set; } = [];

        public IReadOnlyList<IMetadataEpisodeCrossReference> MetadataEpisodeCrossReferences { get; set; } = [];

        public IReadOnlyList<IMetadataSeriesCrossReference> MetadataSeriesCrossReferences { get; set; } = [];

        public IReadOnlyList<IMetadataMovieCrossReference> MetadataMovieCrossReferences { get; set; } = [];

        public IReadOnlyList<IVideo> Videos { get; set; } = [];

        public DateTime CreatedAt { get; set; } = DateTime.UnixEpoch;

        public DateTime LastUpdatedAt { get; set; } = DateTime.UnixEpoch;

        // Most tests leave the series unset.
        public DateTime? LastRefreshedAt => Series?.LastRefreshedAt;
    }

    public sealed class FakeTag(string id, string name) : ITag
    {
        public MetadataGuid ID { get; } = FakeMetadataEntries.ID(MetadataEntityType.Tag, id);

        public string Name { get; } = name;

        public string Overview { get; set; } = $"About {name}.";

        public TagKind Kind { get; set; } = TagKind.Tag;

        public string? Category { get; set; }

        public bool IsSpoiler { get; set; }

        public bool IsRestricted { get; set; }

        public int? Weight { get; set; }
    }

    public sealed class FakeEpisodeLink(int anidbAnimeID, int anidbEpisodeID, MetadataGuid? providerID, int ordering = 0) : IMetadataEpisodeCrossReference
    {
        public int AnidbAnimeID { get; } = anidbAnimeID;

        public int AnidbEpisodeID { get; } = anidbEpisodeID;

        public MetadataGuid? ProviderID { get; } = providerID;

        public MetadataGuid? ProviderParentID { get; set; }

        public MetadataGuid? SeasonID { get; set; }

        public int? SeasonNumber { get; set; }

        public int? EpisodeNumber { get; set; }

        public MetadataEntityType EntityType => MetadataEntityType.Episode;

        public MetadataSource Source => FakeMetadataEntries.Source;

        public MatchRating MatchRating { get; set; } = MatchRating.UserVerified;

        public int Ordering { get; } = ordering;

        public IShokoEpisode? ShokoEpisode => null;

        public IShokoSeries? ShokoSeries => null;

        public IMetadata? Provider => null;

        public Guid? WrittenBy { get; set; }
    }

    #endregion
}
