using System;
using System.Collections.Generic;
using System.Linq;
using Moq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Storage;
using Shoko.Abstractions.Metadata.Stub;
using Shoko.Abstractions.Metadata.Text.Options;
using Shoko.Plugin.Tmdb.Services;

namespace Shoko.Tests.Plugin.Tmdb;

/// <summary>
///   The core's stores, kept in memory, as the TMDb plugin writes and reads
///   them. Each records what was written and reads it back as the core
///   would, through mocks of the read models.
/// </summary>
internal sealed class TmdbFakeStores
{
    #region Data

    /// <summary>The series saved, by ID.</summary>
    public Dictionary<MetadataGuid, MetadataSeriesData> Series { get; } = [];

    /// <summary>The movies saved, by ID.</summary>
    public Dictionary<MetadataGuid, MetadataMovieData> Movies { get; } = [];

    /// <summary>The collections saved, by ID.</summary>
    public Dictionary<MetadataGuid, MetadataCollectionData> Collections { get; } = [];

    /// <summary>The collections removed.</summary>
    public List<MetadataGuid> RemovedCollections { get; } = [];

    /// <summary>The cast set on each entry.</summary>
    public Dictionary<MetadataGuid, IReadOnlyList<MetadataCastData>> Cast { get; } = [];

    /// <summary>The crew set on each entry.</summary>
    public Dictionary<MetadataGuid, IReadOnlyList<MetadataCrewData>> Crew { get; } = [];

    /// <summary>The creators saved, by ID.</summary>
    public Dictionary<MetadataGuid, MetadataCreatorData> Creators { get; } = [];

    /// <summary>The studios saved, by ID.</summary>
    public Dictionary<MetadataGuid, MetadataStudioData> Studios { get; } = [];

    /// <summary>The studios set on each entry.</summary>
    public Dictionary<MetadataGuid, IReadOnlyList<MetadataEntryStudioData>> EntryStudios { get; } = [];

    /// <summary>The networks saved, by ID.</summary>
    public Dictionary<MetadataGuid, MetadataNetworkData> Networks { get; } = [];

    /// <summary>The networks set on each entry.</summary>
    public Dictionary<MetadataGuid, IReadOnlyList<MetadataEntryNetworkData>> EntryNetworks { get; } = [];

    /// <summary>The tags saved, by ID.</summary>
    public Dictionary<MetadataGuid, MetadataTagData> Tags { get; } = [];

    /// <summary>The tags set on each entry.</summary>
    public Dictionary<MetadataGuid, IReadOnlyList<MetadataEntryTagData>> EntryTags { get; } = [];

    /// <summary>The suggestions set on each entry.</summary>
    public Dictionary<MetadataGuid, IReadOnlyList<MetadataSuggestionData>> Suggestions { get; } = [];

    /// <summary>The orderings saved, by ID.</summary>
    public Dictionary<MetadataGuid, MetadataOrderingData> Orderings { get; } = [];

    /// <summary>The orderings removed.</summary>
    public List<MetadataGuid> RemovedOrderings { get; } = [];

    /// <summary>The overviews set through the text manager, by entry.</summary>
    public Dictionary<MetadataGuid, IReadOnlyList<IText>> SetOverviews { get; } = [];

    /// <summary>The language order the text manager hands out.</summary>
    public List<TitleLanguage> LanguageOrder { get; } = [TitleLanguage.EnglishAmerican, TitleLanguage.Romaji, TitleLanguage.Japanese];

    /// <summary>How many times each kind of save ran.</summary>
    public Dictionary<string, int> Saves { get; } = [];

    #endregion

    #region Stores

    /// <summary>
    ///   The stores, as the plugin takes them.
    /// </summary>
    /// <returns>The stores.</returns>
    public TmdbStores Build()
        => new(SeriesStore(), MovieStore(), CollectionStore(), PeopleStore(), StudioStore(), TagStore(), SuggestionStore(), OrderingService(), TextManager());

    private void Count(string kind)
        => Saves[kind] = Saves.GetValueOrDefault(kind) + 1;

    private IMetadataSeriesStore SeriesStore()
    {
        var store = new Mock<IMetadataSeriesStore>(MockBehavior.Strict);
        store.Setup(mock => mock.GetSeries(It.IsAny<MetadataGuid>())).Returns((MetadataGuid id) => Series.TryGetValue(id, out var data) ? ReadSeries(data) : null);
        store.Setup(mock => mock.GetSeason(It.IsAny<MetadataGuid>())).Returns((MetadataGuid id) =>
            Series.Values.SelectMany(series => series.Seasons.Select(season => (series, season))).FirstOrDefault(pair => pair.season.ID == id) is { series: not null } found
                ? ReadSeason(found.series, found.season)
                : null);
        store.Setup(mock => mock.GetEpisode(It.IsAny<MetadataGuid>())).Returns((MetadataGuid id) =>
            Series.Values.SelectMany(series => series.Episodes.Select(episode => (series, episode))).FirstOrDefault(pair => pair.episode.ID == id) is { series: not null } found
                ? ReadEpisode(found.series, found.episode)
                : null);
        store.Setup(mock => mock.SaveSeries(It.IsAny<MetadataSeriesData>())).Returns((MetadataSeriesData data) =>
        {
            Count(nameof(Series));
            Series[data.ID] = data;
            return 1;
        });
        return store.Object;
    }

    private IMetadataMovieStore MovieStore()
    {
        var store = new Mock<IMetadataMovieStore>(MockBehavior.Strict);
        store.Setup(mock => mock.GetMovie(It.IsAny<MetadataGuid>())).Returns((MetadataGuid id) => Movies.TryGetValue(id, out var data) ? ReadMovie(data) : null);
        store.Setup(mock => mock.SaveMovie(It.IsAny<MetadataMovieData>())).Returns((MetadataMovieData data) =>
        {
            Count(nameof(Movies));
            Movies[data.ID] = data;
            return 1;
        });
        return store.Object;
    }

    private IMetadataCollectionStore CollectionStore()
    {
        var store = new Mock<IMetadataCollectionStore>(MockBehavior.Strict);
        store.Setup(mock => mock.GetCollection(It.IsAny<MetadataGuid>())).Returns((MetadataGuid id) =>
            Collections.ContainsKey(id) ? Mock.Of<ICollection>(collection => collection.ID == id) : null);
        store.Setup(mock => mock.SaveCollection(It.IsAny<MetadataCollectionData>())).Returns((MetadataCollectionData data) =>
        {
            Collections[data.ID] = data;
            return 1;
        });
        store.Setup(mock => mock.RemoveCollection(It.IsAny<MetadataGuid>())).Returns((MetadataGuid id) =>
        {
            RemovedCollections.Add(id);
            return Collections.Remove(id) ? 1 : 0;
        });
        return store.Object;
    }

    private IMetadataPeopleStore PeopleStore()
    {
        var store = new Mock<IMetadataPeopleStore>(MockBehavior.Strict);
        store.Setup(mock => mock.GetCast(It.IsAny<MetadataGuid>())).Returns((MetadataGuid entry) =>
            [.. Cast.GetValueOrDefault(entry, []).Select(credit => ReadCast(entry, credit))]);
        store.Setup(mock => mock.GetCrew(It.IsAny<MetadataGuid>())).Returns((MetadataGuid entry) =>
            [.. Crew.GetValueOrDefault(entry, []).Select(credit => ReadCrew(entry, credit))]);
        store.Setup(mock => mock.SetCast(It.IsAny<MetadataGuid>(), It.IsAny<IEnumerable<MetadataCastData>>())).Returns((MetadataGuid entry, IEnumerable<MetadataCastData> cast) =>
        {
            Cast[entry] = [.. cast];
            return 1;
        });
        store.Setup(mock => mock.SetCrew(It.IsAny<MetadataGuid>(), It.IsAny<IEnumerable<MetadataCrewData>>())).Returns((MetadataGuid entry, IEnumerable<MetadataCrewData> crew) =>
        {
            Crew[entry] = [.. crew];
            return 1;
        });
        store.Setup(mock => mock.SaveCreators(It.IsAny<IEnumerable<MetadataCreatorData>>())).Callback((IEnumerable<MetadataCreatorData> creators) =>
        {
            foreach (var creator in creators)
                Creators[creator.ID] = creator;
        });
        return store.Object;
    }

    private IMetadataStudioStore StudioStore()
    {
        var store = new Mock<IMetadataStudioStore>(MockBehavior.Strict);
        store.Setup(mock => mock.SaveStudios(It.IsAny<IEnumerable<MetadataStudioData>>())).Callback((IEnumerable<MetadataStudioData> studios) =>
        {
            foreach (var studio in studios)
                Studios[studio.ID] = studio;
        });
        store.Setup(mock => mock.SetStudios(It.IsAny<MetadataGuid>(), It.IsAny<IEnumerable<MetadataEntryStudioData>>()))
            .Callback((MetadataGuid entry, IEnumerable<MetadataEntryStudioData> studios) => EntryStudios[entry] = [.. studios]);
        store.Setup(mock => mock.SaveNetworks(It.IsAny<IEnumerable<MetadataNetworkData>>())).Callback((IEnumerable<MetadataNetworkData> networks) =>
        {
            foreach (var network in networks)
                Networks[network.ID] = network;
        });
        store.Setup(mock => mock.SetNetworks(It.IsAny<MetadataGuid>(), It.IsAny<IEnumerable<MetadataEntryNetworkData>>()))
            .Callback((MetadataGuid entry, IEnumerable<MetadataEntryNetworkData> networks) => EntryNetworks[entry] = [.. networks]);
        return store.Object;
    }

    private IMetadataTagStore TagStore()
    {
        var store = new Mock<IMetadataTagStore>(MockBehavior.Strict);
        store.Setup(mock => mock.GetTag(It.IsAny<MetadataGuid>())).Returns((MetadataGuid id) =>
            Tags.TryGetValue(id, out var tag) ? Mock.Of<ITag>(read => read.ID == tag.ID && read.Name == tag.Name && read.Kind == tag.Kind) : null);
        store.Setup(mock => mock.SaveTags(It.IsAny<IEnumerable<MetadataTagData>>())).Callback((IEnumerable<MetadataTagData> tags) =>
        {
            Count(nameof(Tags));
            foreach (var tag in tags)
                Tags[tag.ID] = tag;
        });
        store.Setup(mock => mock.SetTags(It.IsAny<MetadataGuid>(), It.IsAny<IEnumerable<MetadataEntryTagData>>()))
            .Callback((MetadataGuid entry, IEnumerable<MetadataEntryTagData> tags) => EntryTags[entry] = [.. tags]);
        return store.Object;
    }

    private IMetadataSuggestionStore SuggestionStore()
    {
        var store = new Mock<IMetadataSuggestionStore>(MockBehavior.Strict);
        store.Setup(mock => mock.SetSuggestions(It.IsAny<MetadataGuid>(), It.IsAny<IEnumerable<MetadataSuggestionData>>()))
            .Callback((MetadataGuid entry, IEnumerable<MetadataSuggestionData> suggestions) => Suggestions[entry] = [.. suggestions]);
        return store.Object;
    }

    private IMetadataOrderingService OrderingService()
    {
        var service = new Mock<IMetadataOrderingService>(MockBehavior.Strict);
        service.Setup(mock => mock.SaveOrdering(It.IsAny<MetadataOrderingData>())).Returns((MetadataOrderingData data) =>
        {
            Orderings[data.ID] = data;
            return ReadOrdering(data);
        });
        service.Setup(mock => mock.GetOrderings(It.IsAny<MetadataGuid>())).Returns((MetadataGuid seriesID) =>
            [.. Orderings.Values.Where(ordering => ordering.SeriesID == seriesID).Select(ReadOrdering)]);
        service.Setup(mock => mock.RemoveOrdering(It.IsAny<MetadataGuid>())).Returns((MetadataGuid id) =>
        {
            RemovedOrderings.Add(id);
            return Orderings.Remove(id);
        });
        return service.Object;
    }

    private IMetadataTextManager TextManager()
    {
        var manager = new Mock<IMetadataTextManager>(MockBehavior.Strict);
        manager.Setup(mock => mock.GetLanguageOrder(It.IsAny<TextKind>(), It.IsAny<MetadataEntityType?>())).Returns(() => LanguageOrder);
        manager.Setup(mock => mock.GetTitles(It.IsAny<MetadataGuid>(), It.IsAny<TextFilteringOptions?>())).Returns((MetadataGuid id, TextFilteringOptions? _) => TitlesOf(id));
        manager.Setup(mock => mock.GetOverviews(It.IsAny<MetadataGuid>(), It.IsAny<TextFilteringOptions?>())).Returns((MetadataGuid id, TextFilteringOptions? _) => OverviewsOf(id));
        manager.Setup(mock => mock.SetOverviews(It.IsAny<MetadataGuid>(), It.IsAny<MetadataSource>(), It.IsAny<IEnumerable<IText>>()))
            .Callback((MetadataGuid id, MetadataSource _, IEnumerable<IText> overviews) => SetOverviews[id] = [.. overviews]);
        return manager.Object;
    }

    #endregion

    #region Reading

    private IReadOnlyList<ITitle> TitlesOf(MetadataGuid id)
        => Series.Values.SelectMany(series => series.Seasons.Select(season => (season.ID, season.Titles)).Concat(series.Episodes.Select(episode => (episode.ID, episode.Titles))))
            .FirstOrDefault(entry => entry.ID == id).Titles ?? [];

    private IReadOnlyList<IText> OverviewsOf(MetadataGuid id)
        => Series.Values.SelectMany(series => series.Seasons.Select(season => (season.ID, season.Overviews)).Concat(series.Episodes.Select(episode => (episode.ID, episode.Overviews))))
            .FirstOrDefault(entry => entry.ID == id).Overviews ?? [];

    private ISeries ReadSeries(MetadataSeriesData data)
    {
        var series = new Mock<ISeries> { DefaultValue = DefaultValue.Empty };
        series.SetupGet(mock => mock.ID).Returns(data.ID);
        series.SetupGet(mock => mock.Seasons).Returns(() => [.. data.Seasons.Select(season => ReadSeason(data, season))]);
        series.SetupGet(mock => mock.Episodes).Returns(() => [.. data.Episodes.Select(episode => ReadEpisode(data, episode))]);
        series.SetupGet(mock => mock.Titles).Returns(data.Titles);
        series.SetupGet(mock => mock.DefaultTitle).Returns(DefaultTitle(data.Titles));
        series.SetupGet(mock => mock.DefaultOverview).Returns(data.Overviews.FirstOrDefault());
        series.SetupGet(mock => mock.OriginalLanguageCode).Returns(data.OriginalLanguageCode);
        series.SetupGet(mock => mock.Restricted).Returns(data.Restricted);
        series.SetupGet(mock => mock.Rating).Returns(data.Rating);
        series.SetupGet(mock => mock.RatingVotes).Returns(data.RatingVotes);
        series.SetupGet(mock => mock.AirDate).Returns(data.AirDate);
        series.SetupGet(mock => mock.Tags).Returns(() => [.. EntryTags.GetValueOrDefault(data.ID, []).Select(tag => Tags[tag.TagID]).Select(tag => Mock.Of<ITag>(read => read.ID == tag.ID && read.Name == tag.Name && read.Kind == tag.Kind))]);
        return series.Object;
    }

    private static ISeason ReadSeason(MetadataSeriesData series, MetadataSeasonData data)
    {
        var season = new Mock<ISeason> { DefaultValue = DefaultValue.Empty };
        season.SetupGet(mock => mock.ID).Returns(data.ID);
        season.SetupGet(mock => mock.SeriesID).Returns(series.ID);
        season.SetupGet(mock => mock.SeasonNumber).Returns(data.SeasonNumber);
        season.SetupGet(mock => mock.Episodes).Returns(() => [.. series.Episodes.Where(episode => episode.SeasonID == data.ID).Select(episode => ReadEpisode(series, episode))]);
        return season.Object;
    }

    private static IEpisode ReadEpisode(MetadataSeriesData series, MetadataEpisodeData data)
    {
        var episode = new Mock<IEpisode> { DefaultValue = DefaultValue.Empty };
        episode.SetupGet(mock => mock.ID).Returns(data.ID);
        episode.SetupGet(mock => mock.SeriesID).Returns(series.ID);
        episode.SetupGet(mock => mock.SeasonID).Returns(data.SeasonID);
        episode.SetupGet(mock => mock.SeasonNumber).Returns(data.SeasonNumber);
        episode.SetupGet(mock => mock.EpisodeNumber).Returns(data.EpisodeNumber);
        episode.SetupGet(mock => mock.Type).Returns(data.Type);
        episode.SetupGet(mock => mock.Rating).Returns(data.Rating);
        episode.SetupGet(mock => mock.RatingVotes).Returns(data.RatingVotes);
        episode.SetupGet(mock => mock.Runtime).Returns(data.Runtime);
        episode.SetupGet(mock => mock.AirDate).Returns(data.AirDate);
        episode.SetupGet(mock => mock.CrossSourceIDs).Returns(data.CrossSourceIDs);
        episode.SetupGet(mock => mock.Titles).Returns(data.Titles);
        episode.SetupGet(mock => mock.DefaultTitle).Returns(DefaultTitle(data.Titles));
        return episode.Object;
    }

    private static IMovie ReadMovie(MetadataMovieData data)
    {
        var movie = new Mock<IMovie> { DefaultValue = DefaultValue.Empty };
        movie.SetupGet(mock => mock.ID).Returns(data.ID);
        movie.SetupGet(mock => mock.Titles).Returns(data.Titles);
        movie.SetupGet(mock => mock.DefaultTitle).Returns(DefaultTitle(data.Titles));
        movie.SetupGet(mock => mock.OriginalLanguageCode).Returns(data.OriginalLanguageCode);
        movie.SetupGet(mock => mock.ReleaseDate).Returns(data.ReleaseDate?.ToDateTime(TimeOnly.MinValue));
        return movie.Object;
    }

    private ICast ReadCast(MetadataGuid entry, MetadataCastData data)
    {
        var cast = new Mock<ICast> { DefaultValue = DefaultValue.Empty };
        cast.SetupGet(mock => mock.ParentID).Returns(entry);
        cast.SetupGet(mock => mock.CreatorID).Returns(data.CreatorID);
        cast.SetupGet(mock => mock.CharacterID).Returns(data.CharacterID);
        cast.SetupGet(mock => mock.Name).Returns(data.Name);
        cast.SetupGet(mock => mock.Description).Returns(data.RoleNotes);
        cast.SetupGet(mock => mock.LanguageCode).Returns(data.LanguageCode ?? string.Empty);
        cast.SetupGet(mock => mock.Creator).Returns(data.CreatorID is { } creatorID ? Mock.Of<ICreator>(creator => creator.ID == creatorID && creator.Name == (data.CreatorName ?? string.Empty)) : null);
        return cast.Object;
    }

    private ICrew ReadCrew(MetadataGuid entry, MetadataCrewData data)
    {
        var crew = new Mock<ICrew> { DefaultValue = DefaultValue.Empty };
        crew.SetupGet(mock => mock.ParentID).Returns(entry);
        crew.SetupGet(mock => mock.CreatorID).Returns(data.CreatorID);
        crew.SetupGet(mock => mock.Name).Returns(data.Name);
        crew.SetupGet(mock => mock.RoleType).Returns(data.RoleType);
        crew.SetupGet(mock => mock.LanguageCode).Returns(data.LanguageCode ?? string.Empty);
        crew.SetupGet(mock => mock.Creator).Returns(Mock.Of<ICreator>(creator => creator.ID == data.CreatorID && creator.Name == (data.CreatorName ?? string.Empty)));
        return crew.Object;
    }

    private static IOrdering ReadOrdering(MetadataOrderingData data)
        => Mock.Of<IOrdering>(ordering => ordering.ID == data.ID && ordering.SeriesID == data.SeriesID && ordering.Name == data.Name);

    private static ITitle DefaultTitle(IReadOnlyList<ITitle> titles)
        => titles.FirstOrDefault(title => title.Type is TitleType.Main) ?? titles.FirstOrDefault()
            ?? new TitleStub { Source = MetadataSource.TMDB, Language = TitleLanguage.Unknown, LanguageCode = "unk", Value = string.Empty };

    #endregion
}
