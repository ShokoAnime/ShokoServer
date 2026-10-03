using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.CrossReferences;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Image.Options;
using Shoko.Abstractions.Metadata.Search;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Storage;
using Shoko.Server.API.Converters;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.API.v3.Models.Metadata;
using Shoko.Server.Models.Metadata;

using AbstractResource = Shoko.Abstractions.Metadata.Resource;
using Resource = Shoko.Server.API.v3.Models.Common.Resource;

namespace Shoko.Server.API.v3.Helpers;

/// <summary>
/// Builds the generic metadata models of the <c>Metadata/{source}</c> routes
/// out of the abstractions alone, the same way for every source.
/// </summary>
/// <remarks>
/// Titles and overviews are read through the text manager by the entry's
/// identifier, so what a user or plugin added to an entry comes along. Images
/// are read through the image manager rather than the entry itself.
/// </remarks>
/// <param name="metadataService">Resolves related entries and resources.</param>
/// <param name="textManager">Reads titles and overviews.</param>
/// <param name="imageManager">Reads images.</param>
/// <param name="studioStore">Counts a plugin source's network entries.</param>
public sealed class MetadataModelBuilder(
    IMetadataService metadataService,
    IMetadataTextManager textManager,
    IImageManager imageManager,
    IMetadataStudioStore studioStore
)
{
    #region Routes

    /// <summary>
    /// The route segment of each kind of entry that has typed routes.
    /// </summary>
    private static readonly IReadOnlyDictionary<MetadataEntityType, string> _kindSegments = new Dictionary<MetadataEntityType, string>
    {
        [MetadataEntityType.Series] = "Series",
        [MetadataEntityType.Season] = "Season",
        [MetadataEntityType.Episode] = "Episode",
        [MetadataEntityType.Movie] = "Movie",
        [MetadataEntityType.Collection] = "Collection",
        [MetadataEntityType.Creator] = "Creator",
        [MetadataEntityType.Character] = "Character",
        [MetadataEntityType.Tag] = "Tag",
        [MetadataEntityType.Studio] = "Studio",
        [MetadataEntityType.Network] = "Network",
    };

    /// <summary>
    /// The route that serves an entry, relative to <c>/api/v3/</c>.
    /// </summary>
    /// <param name="id">The entry.</param>
    /// <returns>The route, with a <c>/</c> in the ID sent as <c>%2F</c>.</returns>
    public static string PathOf(MetadataGuid id)
    {
        ArgumentNullException.ThrowIfNull(id);

        var kind = _kindSegments.TryGetValue(id.EntityType, out var segment) ? segment : id.EntityType.Value;
        return $"Metadata/{id.Source.Value}/{kind}/{Uri.EscapeDataString(id.ID)}";
    }

    #endregion

    #region Entries

    /// <summary>
    /// The minimal view of any entry: who it is, its titles and overviews if
    /// it has them, and its images if asked for.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <param name="include">The extra details asked for.</param>
    /// <param name="language">The languages to keep, or <c>null</c> for all.</param>
    /// <returns>The model.</returns>
    public MetadataEntry Entry(IMetadata entry, IReadOnlySet<MetadataIncludeDetails>? include = null, IReadOnlySet<TitleLanguage>? language = null)
        => Fill(new MetadataEntry(), entry, include, language);

    /// <summary>
    /// A series.
    /// </summary>
    /// <param name="series">The series.</param>
    /// <param name="include">The extra details asked for.</param>
    /// <param name="language">The languages to keep, or <c>null</c> for all.</param>
    /// <returns>The model.</returns>
    public MetadataSeries Series(ISeries series, IReadOnlySet<MetadataIncludeDetails>? include = null, IReadOnlySet<TitleLanguage>? language = null)
    {
        ArgumentNullException.ThrowIfNull(series);

        include ??= new HashSet<MetadataIncludeDetails>();
        return Fill(new MetadataSeries
        {
            AnimeType = series.Type,
            ReleaseStatus = series.ReleaseStatus,
            SourceMaterial = series.SourceMaterial,
            OriginalLanguage = series.OriginalLanguageCode,
            IsRestricted = series.Restricted,
            AirDate = series.AirDate,
            EndDate = series.EndDate,
            Rating = Rating(series.Source, series.Rating, series.RatingVotes),
            Popularity = series.Popularity,
            FavoriteCount = series.FavoriteCount,
            EpisodeCounts = series.EpisodeCounts,
            SeasonCount = series.Seasons.Count(season => !season.IsSpecial),
            Genres = [.. series.Tags.Where(tag => tag.Kind is TagKind.Genre).Select(tag => tag.Name)],
            ShokoSeriesIDs = series.ShokoSeriesIDs,
            CreatedAt = series.CreatedAt.ToUniversalTime(),
            LastUpdatedAt = series.LastUpdatedAt.ToUniversalTime(),
            LastRefreshedAt = series.LastRefreshedAt?.ToUniversalTime(),
            Tags = include.Contains(MetadataIncludeDetails.Tags) ? Tags(series.Tags) : null,
            Studios = include.Contains(MetadataIncludeDetails.Studios) ? [.. series.Studios.Select(Studio)] : null,
            Networks = include.Contains(MetadataIncludeDetails.Networks) ? [.. series.Networks.Select(network => Network(network))] : null,
            Cast = include.Contains(MetadataIncludeDetails.Cast) ? [.. series.Cast.Select(Cast)] : null,
            Crew = include.Contains(MetadataIncludeDetails.Crew) ? [.. series.Crew.Select(Crew)] : null,
            CrossReferences = include.Contains(MetadataIncludeDetails.CrossReferences) ? Links(series.MetadataSeriesCrossReferences) : null,
            Resources = include.Contains(MetadataIncludeDetails.Resources) ? Resources(series) : null,
            ContentRatings = include.Contains(MetadataIncludeDetails.ContentRatings) ? ContentRatings(series.ContentRatings, language) : null,
            YearlySeasons = include.Contains(MetadataIncludeDetails.YearlySeasons) ? series.YearlySeasons.ToV3Dto() : null,
        }, series, include, language);
    }

    /// <summary>
    /// A season.
    /// </summary>
    /// <param name="season">The season.</param>
    /// <param name="include">The extra details asked for.</param>
    /// <param name="language">The languages to keep, or <c>null</c> for all.</param>
    /// <returns>The model.</returns>
    public MetadataSeason Season(ISeason season, IReadOnlySet<MetadataIncludeDetails>? include = null, IReadOnlySet<TitleLanguage>? language = null)
    {
        ArgumentNullException.ThrowIfNull(season);

        include ??= new HashSet<MetadataIncludeDetails>();
        return Fill(new MetadataSeason
        {
            SeriesID = season.SeriesID.ID,
            SeasonNumber = season.SeasonNumber,
            EpisodeCount = season.Episodes.Count,
            CreatedAt = season.CreatedAt.ToUniversalTime(),
            LastUpdatedAt = season.LastUpdatedAt.ToUniversalTime(),
            LastRefreshedAt = season.LastRefreshedAt?.ToUniversalTime(),
            Cast = include.Contains(MetadataIncludeDetails.Cast) ? [.. season.Cast.Select(Cast)] : null,
            Crew = include.Contains(MetadataIncludeDetails.Crew) ? [.. season.Crew.Select(Crew)] : null,
            YearlySeasons = include.Contains(MetadataIncludeDetails.YearlySeasons) ? season.YearlySeasons.ToV3Dto() : null,
        }, season, include, language);
    }

    /// <summary>
    /// An episode.
    /// </summary>
    /// <param name="episode">The episode.</param>
    /// <param name="include">The extra details asked for.</param>
    /// <param name="language">The languages to keep, or <c>null</c> for all.</param>
    /// <returns>The model.</returns>
    public MetadataEpisode Episode(IEpisode episode, IReadOnlySet<MetadataIncludeDetails>? include = null, IReadOnlySet<TitleLanguage>? language = null)
    {
        ArgumentNullException.ThrowIfNull(episode);

        include ??= new HashSet<MetadataIncludeDetails>();
        return Fill(new MetadataEpisode
        {
            SeriesID = episode.SeriesID.ID,
            SeasonID = episode.SeasonID?.ID,
            EpisodeType = episode.Type,
            EpisodeNumber = episode.EpisodeNumber,
            SeasonNumber = episode.SeasonNumber,
            Runtime = episode.Runtime,
            AirDate = episode.AirDate,
            AiredAt = episode.AirDateWithTime is { } airedAt ? AsUtc(airedAt) : null,
            Rating = Rating(episode.Source, episode.Rating, episode.RatingVotes),
            ShokoEpisodeIDs = episode.ShokoEpisodeIDs,
            CreatedAt = episode.CreatedAt.ToUniversalTime(),
            LastUpdatedAt = episode.LastUpdatedAt.ToUniversalTime(),
            LastRefreshedAt = episode.LastRefreshedAt?.ToUniversalTime(),
            Cast = include.Contains(MetadataIncludeDetails.Cast) ? [.. episode.Cast.Select(Cast)] : null,
            Crew = include.Contains(MetadataIncludeDetails.Crew) ? [.. episode.Crew.Select(Crew)] : null,
            CrossReferences = include.Contains(MetadataIncludeDetails.CrossReferences) ? Links(episode.MetadataEpisodeCrossReferences) : null,
            Resources = include.Contains(MetadataIncludeDetails.Resources) ? Resources(episode) : null,
        }, episode, include, language);
    }

    /// <summary>
    /// A movie.
    /// </summary>
    /// <param name="movie">The movie.</param>
    /// <param name="include">The extra details asked for.</param>
    /// <param name="language">The languages to keep, or <c>null</c> for all.</param>
    /// <returns>The model.</returns>
    public MetadataMovie Movie(IMovie movie, IReadOnlySet<MetadataIncludeDetails>? include = null, IReadOnlySet<TitleLanguage>? language = null)
    {
        ArgumentNullException.ThrowIfNull(movie);

        include ??= new HashSet<MetadataIncludeDetails>();
        return Fill(new MetadataMovie
        {
            ReleaseDate = movie.ReleaseDate is { } released ? DateOnly.FromDateTime(released) : null,
            Runtime = movie.Runtime,
            IsRestricted = movie.Restricted,
            IsVideo = movie.Video,
            OriginalLanguage = movie.OriginalLanguageCode,
            Rating = Rating(movie.Source, movie.Rating, movie.RatingVotes),
            Genres = [.. movie.Tags.Where(tag => tag.Kind is TagKind.Genre).Select(tag => tag.Name)],
            ShokoSeriesIDs = movie.ShokoSeriesIDs,
            ShokoEpisodeIDs = movie.ShokoEpisodeIDs,
            CreatedAt = movie.CreatedAt.ToUniversalTime(),
            LastUpdatedAt = movie.LastUpdatedAt.ToUniversalTime(),
            LastRefreshedAt = movie.LastRefreshedAt?.ToUniversalTime(),
            Tags = include.Contains(MetadataIncludeDetails.Tags) ? Tags(movie.Tags) : null,
            Studios = include.Contains(MetadataIncludeDetails.Studios) ? [.. movie.Studios.Select(Studio)] : null,
            Cast = include.Contains(MetadataIncludeDetails.Cast) ? [.. movie.Cast.Select(Cast)] : null,
            Crew = include.Contains(MetadataIncludeDetails.Crew) ? [.. movie.Crew.Select(Crew)] : null,
            CrossReferences = include.Contains(MetadataIncludeDetails.CrossReferences) ? Links(movie.MetadataMovieCrossReferences) : null,
            Resources = include.Contains(MetadataIncludeDetails.Resources) ? Resources(movie) : null,
            ContentRatings = include.Contains(MetadataIncludeDetails.ContentRatings) ? ContentRatings(movie.ContentRatings, language) : null,
            YearlySeasons = include.Contains(MetadataIncludeDetails.YearlySeasons) ? movie.YearlySeasons.ToV3Dto() : null,
        }, movie, include, language);
    }

    /// <summary>
    /// A collection.
    /// </summary>
    /// <param name="collection">The collection.</param>
    /// <param name="members">The collection's stored members.</param>
    /// <param name="include">The extra details asked for.</param>
    /// <param name="language">The languages to keep, or <c>null</c> for all.</param>
    /// <returns>The model.</returns>
    public MetadataCollection Collection(ICollection collection, IReadOnlyList<IMetadata> members, IReadOnlySet<MetadataIncludeDetails>? include = null, IReadOnlySet<TitleLanguage>? language = null)
    {
        ArgumentNullException.ThrowIfNull(collection);
        ArgumentNullException.ThrowIfNull(members);

        return Fill(new MetadataCollection
        {
            MovieCount = members.Count(member => member is IMovie),
            SeriesCount = members.Count(member => member is ISeries),
            CreatedAt = collection.CreatedAt.ToUniversalTime(),
            LastUpdatedAt = collection.LastUpdatedAt.ToUniversalTime(),
            LastRefreshedAt = collection.LastRefreshedAt?.ToUniversalTime(),
        }, collection, include, language);
    }

    /// <summary>
    /// A creator.
    /// </summary>
    /// <param name="creator">The creator.</param>
    /// <param name="include">The extra details asked for.</param>
    /// <returns>The model.</returns>
    public MetadataCreator Creator(ICreator creator, IReadOnlySet<MetadataIncludeDetails>? include = null)
    {
        ArgumentNullException.ThrowIfNull(creator);

        include ??= new HashSet<MetadataIncludeDetails>();
        return Fill(new MetadataCreator
        {
            Title = creator.Name,
            Name = creator.Name,
            OriginalName = creator.OriginalName,
            CreatorType = creator.Type,
            Gender = creator.Gender,
            BirthDay = creator.BirthDay,
            DeathDay = creator.DeathDay,
            PlaceOfBirth = creator.PlaceOfBirth,
            IsRestricted = creator.IsRestricted,
            IsStub = IsStub(creator),
            AlternativeNames = [.. creator.AlternativeNames.Select(name => name.Value).Distinct()],
            CreatedAt = creator.CreatedAt.ToUniversalTime(),
            LastUpdatedAt = creator.LastUpdatedAt.ToUniversalTime(),
            Resources = include.Contains(MetadataIncludeDetails.Resources) ? Resources(creator) : null,
        }, creator, include, null);
    }

    /// <summary>
    /// A character.
    /// </summary>
    /// <param name="character">The character.</param>
    /// <param name="include">The extra details asked for.</param>
    /// <returns>The model.</returns>
    public MetadataCharacter Character(ICharacter character, IReadOnlySet<MetadataIncludeDetails>? include = null)
    {
        ArgumentNullException.ThrowIfNull(character);

        include ??= new HashSet<MetadataIncludeDetails>();
        return Fill(new MetadataCharacter
        {
            Title = character.Name,
            Name = character.Name,
            OriginalName = character.OriginalName,
            CharacterType = character.Type,
            Gender = character.Gender,
            BirthDay = character.BirthDay,
            IsStub = IsStub(character),
            AlternativeNames = [.. character.AlternativeNames.Select(name => name.Value).Distinct()],
            CreatedAt = character.CreatedAt.ToUniversalTime(),
            LastUpdatedAt = character.LastUpdatedAt.ToUniversalTime(),
            Resources = include.Contains(MetadataIncludeDetails.Resources) ? Resources(character) : null,
        }, character, include, null);
    }

    /// <summary>
    /// Fills in what every entry has: who it is, its preferred title and
    /// overview, and the titles, overviews and images when asked for.
    /// </summary>
    /// <typeparam name="TModel">The model's type.</typeparam>
    /// <param name="model">The model, with its own details filled in.</param>
    /// <param name="entry">The entry.</param>
    /// <param name="include">The extra details asked for.</param>
    /// <param name="language">The languages to keep, or <c>null</c> for all.</param>
    /// <returns>The same model, with the shared details filled in.</returns>
    private TModel Fill<TModel>(TModel model, IMetadata entry, IReadOnlySet<MetadataIncludeDetails>? include, IReadOnlySet<TitleLanguage>? language)
        where TModel : MetadataEntry
    {
        ArgumentNullException.ThrowIfNull(entry);

        include ??= new HashSet<MetadataIncludeDetails>();
        var id = entry.ID;
        model.ID = id.ID;
        model.Source = id.Source;
        model.Type = id.EntityType;
        model.Guid = id.ToString();
        model.Path = PathOf(id);
        model.SiteUrl = metadataService.GetSiteUrl(entry);
        if (entry is IWithTitles titled)
        {
            model.Title ??= textManager.GetPreferredTitle(id)?.Value ?? titled.Title;
            if (include.Contains(MetadataIncludeDetails.Titles))
                model.Titles = Titles(id, language);
        }

        if (entry is IWithOverviews)
        {
            model.Overview = textManager.GetPreferredOverview(id)?.Value ?? string.Empty;
            if (include.Contains(MetadataIncludeDetails.Overviews))
                model.Overviews = Overviews(id, language);
        }

        if (entry is IWithImages withImages && include.Contains(MetadataIncludeDetails.Images))
            model.Images = Images(withImages, null, language);

        return model;
    }

    #endregion

    #region Text

    /// <summary>
    /// The text of every title of an entry, from the text manager.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <returns>The titles' text, in no particular order.</returns>
    public IEnumerable<string> TitleValues(IMetadata entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return textManager.GetTitles(entry.ID).Select(title => title.Value);
    }

    /// <summary>
    /// Every title of an entry, the preferred one first, then the default.
    /// </summary>
    /// <param name="id">The entry.</param>
    /// <param name="language">The languages to keep, or <c>null</c> for all.</param>
    /// <param name="includeSynthesized">
    /// Whether to add the made-up title of an untitled episode, when that is
    /// what the preferred title is.
    /// </param>
    /// <returns>The titles.</returns>
    public IReadOnlyList<Title> Titles(MetadataGuid id, IReadOnlySet<TitleLanguage>? language = null, bool includeSynthesized = false)
    {
        ArgumentNullException.ThrowIfNull(id);

        var preferred = textManager.GetPreferredTitle(id);
        var defaultTitle = textManager.GetDefaultTitle(id);
        IEnumerable<ITitle> titles = textManager.GetTitles(id);
        if (includeSynthesized && preferred is { IsSynthesized: true } && !titles.Any(title => IText.Equals(title, preferred)))
            titles = titles.Prepend(preferred);
        if (language is { Count: > 0 })
            titles = titles.Where(title => language.Contains(title.Language));

        return [.. titles
            .Select(title => new Title(title, null, (ITitle?)null)
            {
                Type = title.Type,
                // Compared as texts, since a source may give its default a
                // type the listed title does not carry.
                Default = IText.Equals(title, defaultTitle),
                Preferred = IText.Equals(title, preferred),
                Source = LegacyMetadataSpellings.Of(title.Source),
                Synthesized = title.IsSynthesized ? true : null,
            })
            .OrderByDescending(title => title.Preferred)
            .ThenByDescending(title => title.Default)
            .ThenBy(title => title.Language)];
    }

    /// <summary>
    /// Every overview of an entry, the preferred one first, then the default.
    /// </summary>
    /// <param name="id">The entry.</param>
    /// <param name="language">The languages to keep, or <c>null</c> for all.</param>
    /// <returns>The overviews.</returns>
    public IReadOnlyList<Overview> Overviews(MetadataGuid id, IReadOnlySet<TitleLanguage>? language = null)
    {
        ArgumentNullException.ThrowIfNull(id);

        var preferred = textManager.GetPreferredOverview(id);
        var defaultOverview = textManager.GetDefaultOverview(id);
        IEnumerable<IText> overviews = textManager.GetOverviews(id);
        if (language is { Count: > 0 })
            overviews = overviews.Where(overview => language.Contains(overview.Language));

        return [.. overviews
            .Select(overview => new Overview(overview, null, null)
            {
                Default = IText.Equals(overview, defaultOverview),
                Preferred = IText.Equals(overview, preferred),
                Source = LegacyMetadataSpellings.Of(overview.Source),
            })
            .OrderByDescending(overview => overview.Preferred)
            .ThenByDescending(overview => overview.Default)
            .ThenBy(overview => overview.Language)];
    }

    #endregion

    #region Images

    /// <summary>
    /// An entry's images, grouped by type, each with the links the entry sees
    /// it through.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <param name="options">Which images to keep, or <c>null</c> for all.</param>
    /// <param name="language">The languages to keep, or <c>null</c> for all.</param>
    /// <param name="includeRemoteUrl">Which images to hand out a source URL for.</param>
    /// <returns>The images.</returns>
    public Images Images(IWithImages entry, ImageFilteringOptions? options = null, IReadOnlySet<TitleLanguage>? language = null, RemoteUrlInclusion includeRemoteUrl = RemoteUrlInclusion.False)
    {
        ArgumentNullException.ThrowIfNull(entry);

        return imageManager.GetImagesForEntity(entry, options)
            .ToDto(language, includeRemoteUrl: includeRemoteUrl, remoteUrlTemplate: imageManager.GetTemplateUrlForSource)
            .WithCrossReferences(imageManager.GetCrossReferencesForImageList(entry, options));
    }

    /// <summary>
    /// The first primary image of an entry, such as a portrait or a logo.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <returns>The image, or <c>null</c> when it has none.</returns>
    private Image? PrimaryImage(IWithImages entry)
        => imageManager.GetImagesForEntity(entry, new() { ImageType = ImageEntityType.Primary }).FirstOrDefault() is { } image ? new Image(image) : null;

    /// <summary>
    /// Every primary image of an entry, such as the logos of a studio.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <returns>The images.</returns>
    private IReadOnlyList<Image> PrimaryImages(IWithImages entry)
        => [.. imageManager.GetImagesForEntity(entry, new() { ImageType = ImageEntityType.Primary }).Select(image => new Image(image))];

    #endregion

    #region Classification

    /// <summary>
    /// The tags of an entry, most relevant first.
    /// </summary>
    /// <param name="tags">The entry's tags.</param>
    /// <param name="kind">Only the tags of this kind, or <c>null</c> for all.</param>
    /// <param name="excludeOverviews">Whether to leave the overviews out.</param>
    /// <returns>The tags.</returns>
    public IReadOnlyList<MetadataTag> Tags(IEnumerable<ITag> tags, TagKind? kind = null, bool excludeOverviews = false)
        => [.. tags
            .Where(tag => kind is null || tag.Kind == kind)
            .Select(tag => Tag(tag, tag, excludeOverviews))
            .OrderByDescending(tag => tag.Weight ?? 0)
            .ThenBy(tag => tag.Name, StringComparer.Ordinal)];

    /// <summary>
    /// A tag, and how it applies to the entry it was read through.
    /// </summary>
    /// <param name="tag">The tag.</param>
    /// <param name="onEntry">The tag as an entry carries it, when read through one.</param>
    /// <param name="excludeOverview">Whether to leave the overview out.</param>
    /// <param name="size">How many entries have the tag, when asked for.</param>
    /// <returns>The model.</returns>
    public MetadataTag Tag(ITag tag, ITag? onEntry = null, bool excludeOverview = false, int? size = null)
    {
        ArgumentNullException.ThrowIfNull(tag);

        // The tag an entry carries may say how it applies there; the stored
        // one says what the tag is.
        var stored = onEntry is null ? tag : metadataService.GetEntry<ITag>(tag.ID) ?? tag;
        return new()
        {
            ID = tag.ID.ID,
            Source = tag.ID.Source,
            Name = stored.Name,
            Overview = excludeOverview ? null : stored.Overview,
            Kind = stored.Kind,
            Category = stored.Category,
            IsSpoiler = stored.IsSpoiler,
            IsRestricted = stored.IsRestricted,
            Weight = onEntry?.Weight,
            IsLocalSpoiler = onEntry?.IsSpoiler,
            Size = size,
        };
    }

    /// <summary>
    /// A studio.
    /// </summary>
    /// <param name="studio">The studio.</param>
    /// <returns>The model.</returns>
    public MetadataStudio Studio(IStudio studio)
    {
        ArgumentNullException.ThrowIfNull(studio);

        return new()
        {
            ID = studio.ID.ID,
            Source = studio.ID.Source,
            Name = studio.Name,
            SiteUrl = metadataService.GetSiteUrl(studio),
            OriginalName = studio.OriginalName,
            CountryOfOrigin = studio.CountryOfOrigin,
            StudioType = studio.StudioType,
            IsStub = IsStub(studio),
            Size = studio.Works.Count(),
            Logos = PrimaryImages(studio),
        };
    }

    /// <summary>
    /// A network.
    /// </summary>
    /// <param name="network">The network.</param>
    /// <param name="size">How many series aired on it, when asked for.</param>
    /// <returns>The model.</returns>
    public MetadataNetwork Network(INetwork network, int? size = null)
    {
        ArgumentNullException.ThrowIfNull(network);

        return new()
        {
            ID = network.ID.ID,
            Source = network.ID.Source,
            Name = network.Name,
            CountryOfOrigin = network.CountryOfOrigin,
            SiteUrl = metadataService.GetSiteUrl(network),
            IsStub = IsStub(network),
            Size = size,
            Logos = PrimaryImages(network),
        };
    }

    /// <summary>
    /// Whether a creator, character, studio or network is a stub the core
    /// keeps until its source saves it, read through the entry it is on too.
    /// </summary>
    /// <param name="entity">The entity.</param>
    /// <returns><see langword="true"/> for a stub.</returns>
    internal static bool IsStub(IMetadata entity)
        => entity switch
        {
            IMetadataStubRow row => row.IsStub,
            Metadata_Studio_Entry studio => studio.Studio?.IsStub ?? false,
            Metadata_Network_Entry network => network.Network?.IsStub ?? false,
            _ => false,
        };

    /// <summary>
    /// The series and movies that aired on a network.
    /// </summary>
    /// <param name="network">The network.</param>
    /// <returns>The entries.</returns>
    public IReadOnlyList<MetadataGuid> NetworkEntries(INetwork network)
    {
        ArgumentNullException.ThrowIfNull(network);

        // The store also links an ordering to the network it follows, which aired nothing itself.
        if (!network.ID.Source.IsCore)
            return [
                .. studioStore.GetEntriesForNetwork(network.ID)
                    .Where(entry => entry.EntityType == MetadataEntityType.Series || entry.EntityType == MetadataEntityType.Movie),
            ];

        return [.. metadataService.GetAllSeriesForSource(network.ID.Source)
            .Where(series => series.Networks.Any(other => other.ID == network.ID))
            .Select(series => series.ID)];
    }

    /// <summary>
    /// The content ratings of an entry.
    /// </summary>
    /// <param name="contentRatings">The entry's content ratings.</param>
    /// <param name="language">The languages to keep, or <c>null</c> for all.</param>
    /// <returns>The content ratings.</returns>
    public static IReadOnlyList<ContentRating> ContentRatings(IEnumerable<IContentRating> contentRatings, IReadOnlySet<TitleLanguage>? language = null)
        => [.. contentRatings
            .Where(rating => language is not { Count: > 0 } || language.Contains(rating.Language))
            .Select(rating => new ContentRating(rating))];

    /// <summary>
    /// The external resources of an entry, as the entry gives them.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <returns>The resources.</returns>
    public static IReadOnlyList<Resource> Resources(IWithResources entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        return [.. entry.Resources.Select((AbstractResource resource) => new Resource(resource))];
    }

    /// <summary>
    /// A time the abstractions give in UTC, marked as such however it was
    /// read back.
    /// </summary>
    /// <param name="time">The time, in UTC unless marked local.</param>
    /// <returns>The time in UTC.</returns>
    internal static DateTime AsUtc(DateTime time)
        => time.Kind switch
        {
            DateTimeKind.Utc => time,
            DateTimeKind.Local => time.ToUniversalTime(),
            _ => DateTime.SpecifyKind(time, DateTimeKind.Utc),
        };

    /// <summary>
    /// A rating, when the source gives one.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="value">The rating, on a scale of 1 to 10.</param>
    /// <param name="votes">How many votes it rests on.</param>
    /// <returns>The rating, or <c>null</c> when there is none.</returns>
    internal static Rating? Rating(MetadataSource source, double value, int votes)
        => value > 0 || votes > 0
            ? new() { Value = value, MaxValue = 10, Votes = votes, Source = LegacyMetadataSpellings.Of(source), Type = "User" }
            : null;

    #endregion

    #region Credits

    /// <summary>
    /// A cast credit.
    /// </summary>
    /// <param name="cast">The credit.</param>
    /// <returns>The model.</returns>
    public MetadataRole Cast(ICast cast)
    {
        ArgumentNullException.ThrowIfNull(cast);

        var creator = cast.Creator;
        var character = cast.Character;
        return new()
        {
            Kind = MetadataRoleKind.Cast,
            RoleType = cast.RoleType.ToString(),
            Name = cast.Name,
            OriginalName = string.IsNullOrEmpty(cast.OriginalName) ? null : cast.OriginalName,
            RoleDetails = string.IsNullOrEmpty(cast.Description) ? null : cast.Description,
            DubGroup = string.IsNullOrEmpty(cast.DubGroup) ? null : cast.DubGroup,
            Language = LanguageOf(cast.LanguageCode),
            ParentID = cast.ParentID.ID,
            ParentType = cast.ParentID.EntityType,
            Creator = cast.CreatorID is { } creatorID
                ? new() { ID = creatorID.ID, Name = creator?.Name, OriginalName = creator?.OriginalName, Image = creator is null ? null : PrimaryImage(creator) }
                : null,
            Character = cast.CharacterID is { } characterID
                ? new() { ID = characterID.ID, Name = character?.Name ?? cast.Name, OriginalName = character?.OriginalName, Image = character is null ? null : PrimaryImage(character) }
                : null,
        };
    }

    /// <summary>
    /// A crew credit.
    /// </summary>
    /// <param name="crew">The credit.</param>
    /// <returns>The model.</returns>
    public MetadataRole Crew(ICrew crew)
    {
        ArgumentNullException.ThrowIfNull(crew);

        var creator = crew.Creator;
        return new()
        {
            Kind = MetadataRoleKind.Crew,
            RoleType = crew.RoleType.ToString(),
            Name = crew.Name,
            Language = LanguageOf(crew.LanguageCode),
            ParentID = crew.ParentID.ID,
            ParentType = crew.ParentID.EntityType,
            Creator = new() { ID = crew.CreatorID.ID, Name = creator?.Name, OriginalName = creator?.OriginalName, Image = creator is null ? null : PrimaryImage(creator) },
        };
    }

    /// <summary>
    /// A language code, unless it says the language is unknown.
    /// </summary>
    /// <param name="languageCode">The code.</param>
    /// <returns>The code, or <c>null</c>.</returns>
    private static string? LanguageOf(string? languageCode)
        => string.IsNullOrEmpty(languageCode) || languageCode is "unk" ? null : languageCode;

    #endregion

    #region Links

    /// <summary>
    /// Links from AniDB, by AniDB anime and episode.
    /// </summary>
    /// <param name="links">The links.</param>
    /// <param name="metadataService">Gives the linked entries' pages.</param>
    /// <returns>The models.</returns>
    public static IReadOnlyList<MetadataCrossReference> CrossReferences(IEnumerable<IMetadataCrossReference> links, IMetadataService metadataService)
        => [.. links
            .Select(link => CrossReference(link, metadataService))
            .OrderBy(link => link.AnidbAnimeID)
            .ThenBy(link => link.AnidbEpisodeID ?? 0)
            .ThenBy(link => link.Index)];

    /// <summary>
    /// Links from AniDB, with the linked entries' pages read through this
    /// builder's metadata service.
    /// </summary>
    /// <param name="links">The links.</param>
    /// <returns>The models.</returns>
    private IReadOnlyList<MetadataCrossReference> Links(IEnumerable<IMetadataCrossReference> links)
        => CrossReferences(links, metadataService);

    /// <summary>
    /// A link from AniDB.
    /// </summary>
    /// <param name="link">The link.</param>
    /// <param name="metadataService">Gives the linked entry's page.</param>
    /// <returns>The model.</returns>
    public static MetadataCrossReference CrossReference(IMetadataCrossReference link, IMetadataService metadataService)
    {
        ArgumentNullException.ThrowIfNull(link);
        ArgumentNullException.ThrowIfNull(metadataService);

        var (anidbEpisodeID, parentID, seasonID, seasonNumber, episodeNumber) = link switch
        {
            IMetadataEpisodeCrossReference episode => ((int?)episode.AnidbEpisodeID, episode.ProviderParentID?.ID, episode.SeasonID?.ID, episode.SeasonNumber, episode.EpisodeNumber),
            IMetadataMovieCrossReference movie => (movie.AnidbEpisodeID, null, null, null, null),
            IMetadataSeasonCrossReference season => (null, season.ProviderParentID.ID, null, (int?)season.SeasonNumber, null),
            _ => ((int?)null, (string?)null, (string?)null, (int?)null, (int?)null),
        };
        return new()
        {
            Source = link.Source,
            EntityType = link.EntityType,
            AnidbAnimeID = link.AnidbAnimeID,
            AnidbEpisodeID = anidbEpisodeID,
            ID = link.ProviderID?.ID,
            SiteUrl = link.ProviderID is { } providerID ? metadataService.GetSiteUrl(providerID) : null,
            ParentID = parentID,
            SeasonID = seasonID,
            SeasonNumber = seasonNumber,
            EpisodeNumber = episodeNumber,
            Index = link.Ordering,
            MatchRating = link.MatchRating,
            WrittenBy = link.WrittenBy,
        };
    }

    /// <summary>
    /// A relation from the source's own graph.
    /// </summary>
    /// <param name="relation">The relation, seen from the entry asked about.</param>
    /// <returns>The model.</returns>
    public MetadataRelation Relation(IRelatedMetadata relation)
    {
        ArgumentNullException.ThrowIfNull(relation);

        return new()
        {
            ID = relation.RelatedID.ID,
            Type = relation.RelatedID.EntityType,
            Title = TitleOf(relation.Related),
            RelationType = relation.RelationType,
            Source = relation.Source,
            Verified = relation.Verified,
        };
    }

    /// <summary>
    /// A suggestion, from either end.
    /// </summary>
    /// <param name="suggestion">The suggestion.</param>
    /// <param name="suggestedBy">
    /// Whether the entry asked about is the one suggested, so the other end is
    /// the one suggesting it.
    /// </param>
    /// <returns>The model.</returns>
    public MetadataSuggestion Suggestion(ISuggestedMetadata suggestion, bool suggestedBy = false)
    {
        ArgumentNullException.ThrowIfNull(suggestion);

        var otherID = suggestedBy ? suggestion.BaseID : suggestion.SuggestedID;
        var other = suggestedBy ? suggestion.Base : suggestion.Suggested;
        return new()
        {
            ID = otherID.ID,
            Type = otherID.EntityType,
            Title = TitleOf(other),
            Kind = suggestion.Kind,
            Source = suggestion.Source,
            Order = suggestion.Order,
            ApprovalRating = suggestion.ApprovalRating,
            Votes = suggestion.Votes,
            Score = suggestion.Score,
        };
    }

    /// <summary>
    /// Where an episode sits in one ordering.
    /// </summary>
    /// <param name="ordering">The episode's place in the ordering.</param>
    /// <returns>The model.</returns>
    public static MetadataEpisodeOrdering EpisodeOrdering(IEpisodeOrderingInformation ordering)
    {
        ArgumentNullException.ThrowIfNull(ordering);

        return new()
        {
            OrderingID = ordering.OrderingID.ToString(),
            SeasonID = ordering.SeasonID?.ToString(),
            SeasonNumber = ordering.SeasonNumber,
            EpisodeNumber = ordering.EpisodeNumber,
            EpisodeType = ordering.EpisodeType,
            AirsBeforeSeasonNumber = ordering.AirsBeforeSeasonNumber,
            AirsBeforeEpisodeNumber = ordering.AirsBeforeEpisodeNumber,
            AirsAfterSeasonNumber = ordering.AirsAfterSeasonNumber,
            AirsAfterEpisodeID = ordering.AirsAfterEpisodeID?.ToString(),
            AirsBeforeEpisodeID = ordering.AirsBeforeEpisodeID?.ToString(),
            IsDefault = ordering.IsDefault,
            IsPreferred = ordering.IsPreferred,
            CreatedAt = ordering.CreatedAt.ToUniversalTime(),
            LastUpdatedAt = ordering.LastUpdatedAt.ToUniversalTime(),
        };
    }

    /// <summary>
    /// The preferred title of an entry, if it has titles.
    /// </summary>
    /// <param name="entry">The entry, if it is stored.</param>
    /// <returns>The title, or <c>null</c>.</returns>
    private string? TitleOf(IMetadata? entry)
        => entry is IWithTitles titled ? textManager.GetPreferredTitle(entry.ID)?.Value ?? titled.Title : null;

    #endregion

    #region Search

    /// <summary>
    /// A stored series, as a provider's search would have answered it.
    /// </summary>
    /// <param name="series">The series.</param>
    /// <returns>The search result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="series"/> is <see langword="null"/>.</exception>
    public MetadataSeriesSearchResult SearchResult(ISeries series)
    {
        ArgumentNullException.ThrowIfNull(series);

        var (year, season) = series.YearlySeasons.Count > 0 ? series.YearlySeasons[0] : default;
        return new()
        {
            ID = series.ID,
            Title = textManager.GetPreferredTitle(series.ID)?.Value ?? series.Title,
            OriginalTitle = OriginalTitle(series, series.OriginalLanguageCode),
            OriginalLanguageCode = series.OriginalLanguageCode,
            Overview = textManager.GetPreferredOverview(series.ID)?.Value ?? series.PreferredOverview?.Value,
            IsRestricted = series.Restricted,
            UserRating = series.Rating > 0 ? (decimal)series.Rating : null,
            UserVotes = series.RatingVotes > 0 ? series.RatingVotes : null,
            PosterUrl = RemoteUrlOf(series, ImageEntityType.Primary),
            BackdropUrl = RemoteUrlOf(series, ImageEntityType.Backdrop),
            Genres = [.. series.Tags.Where(tag => tag.Kind is TagKind.Genre).Select(tag => tag.Name)],
            FirstAiredAt = series.AirDate,
            Type = series.Type,
            Season = year > 0 ? season : null,
            SeasonYear = year > 0 ? year : null,
            EpisodeCount = series.EpisodeCounts.Episodes > 0 ? series.EpisodeCounts.Episodes : null,
        };
    }

    /// <summary>
    /// A stored movie, as a provider's search would have answered it.
    /// </summary>
    /// <param name="movie">The movie.</param>
    /// <returns>The search result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="movie"/> is <see langword="null"/>.</exception>
    public MetadataMovieSearchResult SearchResult(IMovie movie)
    {
        ArgumentNullException.ThrowIfNull(movie);

        return new()
        {
            ID = movie.ID,
            Title = textManager.GetPreferredTitle(movie.ID)?.Value ?? movie.Title,
            OriginalTitle = OriginalTitle(movie, movie.OriginalLanguageCode),
            OriginalLanguageCode = movie.OriginalLanguageCode,
            Overview = textManager.GetPreferredOverview(movie.ID)?.Value ?? movie.PreferredOverview?.Value,
            IsRestricted = movie.Restricted,
            UserRating = movie.Rating > 0 ? (decimal)movie.Rating : null,
            UserVotes = movie.RatingVotes > 0 ? movie.RatingVotes : null,
            PosterUrl = RemoteUrlOf(movie, ImageEntityType.Primary),
            BackdropUrl = RemoteUrlOf(movie, ImageEntityType.Backdrop),
            Genres = [.. movie.Tags.Where(tag => tag.Kind is TagKind.Genre).Select(tag => tag.Name)],
            ReleasedAt = movie.ReleaseDate is { } released ? new PartialDateOnly(DateOnly.FromDateTime(released)) : null,
            IsStandaloneVideo = movie.Video,
        };
    }

    /// <summary>
    /// An entry's title in its original language, else its default title.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <param name="languageCode">The entry's original language, if known.</param>
    /// <returns>The title.</returns>
    private static string OriginalTitle(IWithTitles entry, string? languageCode)
        => (string.IsNullOrEmpty(languageCode) ? null : entry.Titles.FirstOrDefault(title => string.Equals(title.LanguageCode, languageCode, StringComparison.OrdinalIgnoreCase))?.Value)
            ?? entry.DefaultTitle.Value;

    /// <summary>
    /// Where an entry's preferred or first image of a type can be fetched
    /// from its source.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <param name="imageType">The type of image.</param>
    /// <returns>The link, or <c>null</c> when there is no image or no link.</returns>
    private string? RemoteUrlOf(IWithImages entry, ImageEntityType imageType)
    {
        var images = imageManager.GetImagesForEntity(entry, new() { ImageType = imageType });
        if ((images.FirstOrDefault(image => image.IsPreferred) ?? images.FirstOrDefault()) is not { } image)
            return null;

        return RemoteImageUrl.Resolve(imageManager.GetTemplateUrlForSource(image.Source), image.ResourceID, image.IsAvailable, RemoteUrlInclusion.True);
    }

    #endregion
}
