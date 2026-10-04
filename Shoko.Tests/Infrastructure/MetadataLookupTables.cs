using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Moq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Providers;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Storage;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server.Models.Airing;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.CrossReference;
using Shoko.Server.Models.Metadata;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Cached.Airing;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Server.Repositories.Cached.Metadata;
using Shoko.Server.Repositories.Cached.Metadata.Text;
using Shoko.Server.Services;

namespace Shoko.Tests.Infrastructure;

/// <summary>
/// Every table <see cref="MetadataService.GetEntry(MetadataGuid)"/> reads, in
/// memory and holding one entry of each kind of each core source, and the
/// metadata service built over them.
/// </summary>
/// <remarks>
/// The core's tables are seeded here, and TMDB's show 5, movies 600 and 601
/// and collection 700 in the stores' tables; a plugin source's series, movies
/// and orderings are saved by the test through the stores, which reach for the
/// <see cref="RepoFactory"/> statics, so such a test opens <see cref="Scope"/>.
/// </remarks>
public sealed class MetadataLookupTables
{
    #region Seeded IDs

    public const string VideoHash = "0123456789ABCDEF0123456789ABCDEF";

    public const long VideoSize = 1234;

    public static readonly Guid ChannelID = new("6a3b1f0e-7c1d-4e1a-9b3c-2d4e5f607182");

    #endregion

    private readonly CacheOnlyRowWriter _writer = new();

    #region Repositories

    public Metadata_SeriesRepository Series { get; }
        = CachedRepo.Build<Metadata_SeriesRepository, int, Metadata_Series>(
            row => row.Metadata_SeriesID,
            new Metadata_Series { Metadata_SeriesID = 500, Source = MetadataSource.TMDB, ProviderID = "5" }
        );

    public Metadata_SeasonRepository Seasons { get; }
        = CachedRepo.Build<Metadata_SeasonRepository, int, Metadata_Season>(row => row.Metadata_SeasonID);

    public Metadata_EpisodeRepository Episodes { get; }
        = CachedRepo.Build<Metadata_EpisodeRepository, int, Metadata_Episode>(row => row.Metadata_EpisodeID);

    public Metadata_MovieRepository Movies { get; }
        = CachedRepo.Build<Metadata_MovieRepository, int, Metadata_Movie>(
            row => row.Metadata_MovieID,
            new Metadata_Movie { Metadata_MovieID = 600, Source = MetadataSource.TMDB, ProviderID = "600" },
            new Metadata_Movie { Metadata_MovieID = 601, Source = MetadataSource.TMDB, ProviderID = "601" }
        );

    public TextCache Texts { get; } = new();

    public Metadata_CollectionRepository Collections { get; }
        = CachedRepo.Build<Metadata_CollectionRepository, int, Metadata_Collection>(
            row => row.Metadata_CollectionID,
            new Metadata_Collection { Metadata_CollectionID = 700, Source = MetadataSource.TMDB, ProviderID = "700" }
        );

    public Metadata_Collection_MemberRepository CollectionMembers { get; }
        = CachedRepo.Build<Metadata_Collection_MemberRepository, int, Metadata_Collection_Member>(
            row => row.Metadata_Collection_MemberID,
            new Metadata_Collection_Member { Metadata_Collection_MemberID = 700, Source = MetadataSource.TMDB, CollectionID = "700", MemberType = MetadataEntityType.Movie, MemberID = "600" }
        );

    public Metadata_CreatorRepository Creators { get; }
        = CachedRepo.Build<Metadata_CreatorRepository, int, Metadata_Creator>(row => row.Metadata_CreatorID);

    private readonly Metadata_CharacterRepository _characters
        = CachedRepo.Build<Metadata_CharacterRepository, int, Metadata_Character>(row => row.Metadata_CharacterID);

    private readonly Metadata_TagRepository _tags
        = CachedRepo.Build<Metadata_TagRepository, int, Metadata_Tag>(row => row.Metadata_TagID);

    private readonly Metadata_StudioRepository _studios
        = CachedRepo.Build<Metadata_StudioRepository, int, Metadata_Studio>(row => row.Metadata_StudioID);

    public Metadata_NetworkRepository Networks { get; }
        = CachedRepo.Build<Metadata_NetworkRepository, int, Metadata_Network>(row => row.Metadata_NetworkID);

    #endregion

    #region Stores and Services

    public MetadataSeriesStore SeriesStore { get; }

    public MetadataMovieStore MovieStore { get; }

    public MetadataCollectionStore CollectionStore { get; }

    public MetadataPeopleStore PeopleStore { get; }

    public MetadataTagStore TagStore { get; }

    public MetadataStudioStore StudioStore { get; }

    public Mock<IMetadataCrossReferenceStore> CrossReferences { get; } = new();

    public OrderingTables Orderings { get; } = new();

    public MetadataOrderingService OrderingService { get; }

    public Mock<ILogger<MetadataService>> Logger { get; } = new();

    /// <summary>
    /// The registered providers, none unless a test adds some.
    /// </summary>
    public Mock<IMetadataProviderManager> ProviderManager { get; } = new();

    /// <summary>
    /// The providers <see cref="ProviderManager"/> lists.
    /// </summary>
    public List<MetadataProviderInfo> Providers { get; } = [];

    public MetadataService Service { get; }

    /// <summary>
    /// The text store every store here writes through.
    /// </summary>
    public MetadataTextStore TextStore { get; }

    #endregion

    public MetadataLookupTables()
    {
        ProviderManager.SetupGet(manager => manager.MetadataProviders).Returns(() => Providers);
        var texts = TextStore = new(Texts, _writer);
        Orderings.TextStore = texts;
        var contentRatings = CachedRepo.Build<Metadata_ContentRatingRepository, int, Metadata_ContentRating>(row => row.Metadata_ContentRatingID);
        var shokoSeries = CachedRepo.Build<AnimeSeriesRepository, int, AnimeSeries>(
            series => series.AnimeSeriesID,
            new AnimeSeries { AnimeSeriesID = 3, AniDB_ID = 30, AnimeGroupID = 2 }
        );
        SeriesStore = new(
            Series,
            Seasons,
            Episodes,
            contentRatings,
            texts,
            NoCleanup.Build(),
            new Lazy<MetadataOrderingService>(() => OrderingService!),
            new Lazy<IMetadataCrossReferenceStore>(() => NoLinks.Build().Object),
            new Mock<IQueueScheduler>().Object,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<MetadataSeriesStore>.Instance
        );
        MovieStore = new(Movies, contentRatings, texts, NoCleanup.Build());
        CollectionStore = new(
            Collections,
            CollectionMembers,
            texts,
            NoCleanup.Build()
        );
        PeopleStore = new(
            Creators,
            _characters,
            new InMemoryCastRepository(),
            new InMemoryCrewRepository(),
            texts,
            _writer
        );
        TagStore = new(_tags, CachedRepo.Build<Metadata_Tag_EntryRepository, int, Metadata_Tag_Entry>(row => row.Metadata_Tag_EntryID), _writer, texts);
        StudioStore = new(
            _studios,
            CachedRepo.Build<Metadata_Studio_EntryRepository, int, Metadata_Studio_Entry>(row => row.Metadata_Studio_EntryID),
            Networks,
            CachedRepo.Build<Metadata_Network_EntryRepository, int, Metadata_Network_Entry>(row => row.Metadata_Network_EntryID),
            _writer,
            texts
        );
        Orderings.StudioStore = StudioStore;
        OrderingService = Orderings.Build(() => Service!);
        Service = new(
            CachedRepo.Build<AnimeGroupRepository, int, AnimeGroup>(
                group => group.AnimeGroupID,
                [new AnimeGroup { AnimeGroupID = 1 }, new AnimeGroup { AnimeGroupID = 2, AnimeGroupParentID = 1 }]
            ),
            shokoSeries,
            CachedRepo.Build<AnimeEpisodeRepository, int, AnimeEpisode>(episode => episode.AnimeEpisodeID, new AnimeEpisode { AnimeEpisodeID = 4, AnimeSeriesID = 3 }),
            CachedRepo.Build<VideoLocalRepository, int, VideoLocal>(video => video.VideoLocalID, new VideoLocal { VideoLocalID = 5, Hash = VideoHash, FileSize = VideoSize }),
            CachedRepo.Build<JMMUserRepository, int, JMMUser>(user => user.JMMUserID, new JMMUser { JMMUserID = 6, Username = "User" }),
            CachedRepo.Build<FilterPresetRepository, int, FilterPreset>(filter => filter.FilterPresetID, new FilterPreset { FilterPresetID = 7, Name = "Filter" }),
            CachedRepo.Build<AiringChannelRepository, int, AiringChannel>(channel => channel.AiringChannelID, new AiringChannel { AiringChannelID = 1, ChannelID = ChannelID }),
            CachedRepo.Build<AniDB_AnimeRepository, int, AniDB_Anime>(anime => anime.AniDB_AnimeID, new AniDB_Anime { AniDB_AnimeID = 1, AnimeID = 30 }),
            CachedRepo.Build<AniDB_EpisodeRepository, int, AniDB_Episode>(episode => episode.AniDB_EpisodeID, new AniDB_Episode { AniDB_EpisodeID = 1, EpisodeID = 300, AnimeID = 30 }),
            CachedRepo.Build<AniDB_CreatorRepository, int, AniDB_Creator>(creator => creator.AniDB_CreatorID, new AniDB_Creator { AniDB_CreatorID = 1, CreatorID = 40, Name = "Creator" }),
            CachedRepo.Build<AniDB_CharacterRepository, int, AniDB_Character>(character => character.AniDB_CharacterID, new AniDB_Character { AniDB_CharacterID = 1, CharacterID = 41 }),
            CachedRepo.Build<AniDB_TagRepository, int, AniDB_Tag>(tag => tag.AniDB_TagID, new AniDB_Tag { AniDB_TagID = 1, TagID = 42 }),
            SeriesStore,
            MovieStore,
            CollectionStore,
            PeopleStore,
            TagStore,
            StudioStore,
            OrderingService,
            CachedRepo.Build<CustomTagRepository, int, CustomTag>(tag => tag.CustomTagID, new CustomTag { CustomTagID = 8, TagName = "Custom" }),
            CachedRepo.Build<CrossRef_CustomTagRepository, int, CrossRef_CustomTag>(xref => xref.CrossRef_CustomTagID),
            CrossReferences.Object,
            new Lazy<IMetadataProviderManager>(() => ProviderManager.Object),
            Logger.Object
        );
    }

    /// <summary>
    /// Installs the store tables into the <see cref="RepoFactory"/> statics,
    /// for a test that saves through the stores.
    /// </summary>
    /// <returns>The scope, which puts the statics back when disposed.</returns>
    public RepoFactoryScope Scope()
        => new RepoFactoryScope().Set(Series).Set(Seasons).Set(Episodes).Set(Movies).Set(Texts).Set(Collections).Set(CollectionMembers);

    /// <summary>
    /// Stores one creator, character, tag, studio, network and collection of
    /// a plugin source, each under the ID <c>1</c>.
    /// </summary>
    /// <param name="source">The plugin source.</param>
    public void StorePeopleTagsStudiosAndCollections(MetadataSource source)
    {
        Creators.Cache.Update(new Metadata_Creator { Metadata_CreatorID = 1, Source = source, ProviderID = "1", Name = "Creator" });
        _characters.Cache.Update(new Metadata_Character { Metadata_CharacterID = 1, Source = source, ProviderID = "1", Name = "Character" });
        _tags.Cache.Update(new Metadata_Tag { Metadata_TagID = 1, Source = source, ProviderID = "1", Name = "Tag", Kind = TagKind.Genre });
        _studios.Cache.Update(new Metadata_Studio { Metadata_StudioID = 1, Source = source, ProviderID = "1", Name = "Studio" });
        Networks.Cache.Update(new Metadata_Network { Metadata_NetworkID = 1, Source = source, ProviderID = "1", Name = "Network" });
        Collections.Cache.Update(new Metadata_Collection { Metadata_CollectionID = 1, Source = source, ProviderID = "1" });
    }

    /// <summary>
    /// Saves a series of <see cref="TestSources.Plugin"/> with a season and
    /// an episode, a movie and a global ordering with one group through the
    /// stores, and stores one of each of the other kinds. The test must have
    /// opened <see cref="Scope"/>.
    /// </summary>
    public void StorePluginEntries()
    {
        static MetadataGuid ID(MetadataEntityType entityType, string id)
            => new(TestSources.Plugin, entityType, id);

        var seriesID = ID(MetadataEntityType.Series, "s1");
        SeriesStore.SaveSeries(new()
        {
            ID = seriesID,
            Seasons = [new() { ID = ID(MetadataEntityType.Season, "s1-1"), SeasonNumber = 1 }],
            Episodes = [new MetadataEpisodeData { ID = ID(MetadataEntityType.Episode, "e1"), SeasonID = ID(MetadataEntityType.Season, "s1-1"), EpisodeNumber = 1 }],
        });
        MovieStore.SaveMovie(new() { ID = ID(MetadataEntityType.Movie, "m1") });
        OrderingService.SaveOrdering(new()
        {
            ID = ID(MetadataEntityType.Ordering, "o1"),
            SeriesID = seriesID,
            Titles = TestTexts.Named("Theirs"),
            Groups = [new() { ID = ID(MetadataEntityType.Season, "g1"), Titles = TestTexts.Named("All"), Episodes = [ID(MetadataEntityType.Episode, "e1")] }],
        });
        StorePeopleTagsStudiosAndCollections(TestSources.Plugin);
    }
}
