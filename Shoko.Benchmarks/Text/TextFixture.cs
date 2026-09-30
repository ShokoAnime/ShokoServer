using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Shoko.Abstractions.Core.Services;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Filtering.Services;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Stub;
using Shoko.Server.Filters;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.CrossReference;
using Shoko.Server.Models.Interfaces;
using Shoko.Server.Models.Metadata;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Models.TMDB;
using Shoko.Server.Providers.AniDB.HTTP;
using Shoko.Server.Providers.AniDB.HTTP.GetAnime;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Server.Repositories.Cached.Metadata;
using Shoko.Server.Repositories.Cached.Metadata.Text;
using Shoko.Server.Repositories.Cached.TMDB;
using Shoko.Server.Server;
using Shoko.Server.Services;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

using AnidbTextListing = Shoko.Server.Providers.AniDB.AnidbTextListing;

#pragma warning disable CS0618
namespace Benchmarks.Text;

/// <summary>
///   A synthetic library of <see cref="AnimeCount"/> anime with their AniDB and TMDB text, installed into the
///   <see cref="RepoFactory"/> statics and <see cref="ISystemService.StaticServices"/> so the real text code runs
///   with no database.
/// </summary>
/// <remarks>
///   The rows are generated from a fixed seed, so every run and every commit sees the same library. The mix per anime
///   is close to a real library's (about five titles, fifteen episodes with two titles each, most TV anime linked to a
///   TMDB show whose titles and overviews come in up to ten languages). No real title is used, so the fixture can be
///   committed. Installing is done once per process, because <see cref="ISystemService.StaticServices"/> can only be
///   set once.
/// </remarks>
public sealed class TextFixture
{
    #region Shape

    /// <summary>
    ///   The number of anime in the library.
    /// </summary>
    public const int AnimeCount = 2_000;

    private const int RandomSeed = 20260926;

    private static readonly string[] _syllables =
    [
        "ka", "ki", "ku", "ke", "ko", "sa", "shi", "su", "se", "so", "ta", "chi", "tsu", "te", "to", "na", "ni", "no",
        "ha", "hi", "fu", "ho", "ma", "mi", "mu", "me", "mo", "ya", "yu", "yo", "ra", "ri", "ru", "re", "ro", "wa", "n",
        "ga", "gi", "go", "za", "ji", "zu", "da", "do", "ba", "bi", "bu", "pa", "kyo", "sho", "ryu",
    ];

    private static readonly string[] _words =
    [
        "Sky", "Blade", "Academy", "Dream", "Star", "Night", "Summer", "Girl", "Hero", "Magic", "Tale", "Legend", "World",
        "Heart", "Garden", "Moon", "Sword", "Knight", "City", "Ocean", "Spirit", "Rain", "Winter", "Journey", "Kingdom",
        "Song", "Shadow", "Fire", "Crystal", "Dragon", "School", "Festival", "Memory", "Promise", "Future", "Angel",
        "Demon", "Eternal", "Last", "First", "Little", "Wild", "Silent", "Broken", "Golden", "Crimson", "Blue", "White",
        "Lost", "Hidden", "Secret", "Endless", "Brave", "Gentle", "Iron", "Paper", "Glass", "Distant", "Falling", "Rising",
    ];

    private const string Kanji = "空夢星夜夏少女英雄魔法物語伝説世界心庭月剣騎士街海精霊雨冬旅王国歌影火水晶竜学園祭記憶約束未来天使悪魔永遠";

    private const string Kana = "あいうえおかきくけこさしすせそたちつてとなにぬねのはひふへほまみむめもやゆよらりるれろわんアイウエオカキクケコサシスセソ";

    private const string Hangul = "가나다라마바사아자차카타파하별꿈밤여름소녀영웅마법이야기";

    private const string Cyrillic = "абвгдежзиклмнопрстуфхцчшщыэюя";

    private static readonly (string Language, string Country)[] _tmdbTitleLanguages =
        [("en", "US"), ("ja", "JP"), ("fr", "FR"), ("de", "DE"), ("es", "ES"), ("ko", "KR"), ("zh", "CN"), ("pt", "BR")];

    private static readonly (string Language, string Country)[] _tmdbOverviewLanguages =
        [.. _tmdbTitleLanguages, ("it", "IT"), ("ru", "RU")];

    #endregion

    #region Rows

    public List<AniDB_Anime> Anime { get; } = [];

    public List<AnidbTitle> AnimeTitles { get; } = [];

    public List<AniDB_Episode> AnidbEpisodes { get; } = [];

    public List<AnidbTitle> EpisodeTitles { get; } = [];

    public List<AnimeSeries> Series { get; } = [];

    /// <summary>
    ///   The text store over the fixture's text cache, set once installed.
    /// </summary>
    public MetadataTextStore TextStore { get; private set; } = null!;

    public List<AnimeEpisode> Episodes { get; } = [];

    public List<AnimeGroup> Groups { get; } = [];

    public List<TMDB_Show> TmdbShows { get; } = [];

    public List<TMDB_Episode> TmdbEpisodes { get; } = [];

    public List<TMDB_Movie> TmdbMovies { get; } = [];

    public List<TmdbText> TmdbTitles { get; } = [];

    public List<TmdbText> TmdbOverviews { get; } = [];

    public List<CrossRef_AniDB_Metadata_Series> SeriesLinks { get; } = [];

    public List<CrossRef_AniDB_Metadata_Movie> MovieLinks { get; } = [];

    public List<CrossRef_AniDB_Metadata_Episode> EpisodeLinks { get; } = [];

    /// <summary>
    ///   Ten search queries over the library: whole titles, partial words, romaji with typos, Japanese, and one that
    ///   matches nothing.
    /// </summary>
    public List<string> Queries { get; } = [];

    /// <summary>
    ///   The user the searches run as, with nothing hidden.
    /// </summary>
    public JMMUser User { get; } = new() { JMMUserID = 1, Username = "bench", IsAdmin = 1 };

    #endregion

    #region Installing

    private static readonly Lazy<TextFixture> _instance = new(() => new TextFixture().Install());

    /// <summary>
    ///   The library, generated and installed on first use.
    /// </summary>
    public static TextFixture Instance => _instance.Value;

    private TextFixture()
    {
        var random = new Random(RandomSeed);
        Generate(random);
        PickQueries(random);
    }

    private TextFixture Install()
    {
        var settings = new ServerSettings();
        settings.Language.SeriesTitleLanguageOrder = ["en", "x-jat", "x-main"];
        ISettingsProvider.Instance = new BenchmarkSettingsProvider(settings);

        var anime = Set(Build<AniDB_AnimeRepository, AniDB_Anime>(Anime, a => a.AniDB_AnimeID));
        var anidbEpisodes = Set(Build<AniDB_EpisodeRepository, AniDB_Episode>(AnidbEpisodes, e => e.AniDB_EpisodeID));
        var series = Set(Build<AnimeSeriesRepository, AnimeSeries>(Series, s => s.AnimeSeriesID));
        var episodes = Set(Build<AnimeEpisodeRepository, AnimeEpisode>(Episodes, e => e.AnimeEpisodeID));
        var groups = Set(Build<AnimeGroupRepository, AnimeGroup>(Groups, g => g.AnimeGroupID));
        var shows = Set(Build<TMDB_ShowRepository, TMDB_Show>(TmdbShows, s => s.Id));
        var seasons = Set(Build<TMDB_SeasonRepository, TMDB_Season>([], s => s.Id));
        var tmdbEpisodes = Set(Build<TMDB_EpisodeRepository, TMDB_Episode>(TmdbEpisodes, e => e.Id));
        var movies = Set(Build<TMDB_MovieRepository, TMDB_Movie>(TmdbMovies, m => m.Id));
        var collections = Set(Build<TMDB_CollectionRepository, TMDB_Collection>([], c => c.Id));
        var seriesLinks = Set(Build<CrossRef_AniDB_Metadata_SeriesRepository, CrossRef_AniDB_Metadata_Series>(
            SeriesLinks, x => x.CrossRef_AniDB_Metadata_SeriesID));
        var movieLinks = Set(Build<CrossRef_AniDB_Metadata_MovieRepository, CrossRef_AniDB_Metadata_Movie>(
            MovieLinks, x => x.CrossRef_AniDB_Metadata_MovieID));
        var episodeLinks = Set(Build<CrossRef_AniDB_Metadata_EpisodeRepository, CrossRef_AniDB_Metadata_Episode>(
            EpisodeLinks, x => x.CrossRef_AniDB_Metadata_EpisodeID));
        var texts = Set(new TextCache());
        var storedEpisodes = Set(Build<Metadata_EpisodeRepository, Metadata_Episode>([], e => e.Metadata_EpisodeID));
        Set(Build<AniDB_Anime_TagRepository, AniDB_Anime_Tag>([], t => t.AniDB_Anime_TagID));
        Set(Build<AniDB_TagRepository, AniDB_Tag>([], t => t.AniDB_TagID));
        Set(Build<CustomTagRepository, CustomTag>([], t => t.CustomTagID));
        Set(Build<CrossRef_CustomTagRepository, CrossRef_CustomTag>([], x => x.CrossRef_CustomTagID));

        var crossReferences = new MetadataCrossReferenceStore(seriesLinks, movieLinks, episodeLinks, storedEpisodes);
        var metadataService = new MetadataService(groups, series, episodes, null!, null!, null!, null!, anime, anidbEpisodes, null!, null!, null!,
            shows, seasons, null!, tmdbEpisodes, movies, collections, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!,
            null!, crossReferences, NullLogger<MetadataService>.Instance);
        var textStore = new MetadataTextStore(texts, new CacheOnlyWriter());
        TextStore = textStore;
        var textManager = new MetadataTextManager(metadataService, textStore, NullLogger<MetadataTextManager>.Instance);

        var services = new ServiceCollection();
        services.AddSingleton<IMetadataService>(metadataService);
        services.AddSingleton<IMetadataTextManager>(textManager);
        services.AddSingleton<IFuzzySearchService, FuzzySearchService>();
        ISystemService.StaticServices = services.BuildServiceProvider();

        SeedAnidbText(textStore);
        SeedTmdbText(textStore);
        return this;
    }

    /// <summary>
    ///   Stores each AniDB anime's titles and its episodes' titles as the AniDB import does, which leaves out an
    ///   episode's generic title with its own number.
    /// </summary>
    /// <param name="store">The text store over the fixture's text cache.</param>
    private void SeedAnidbText(MetadataTextStore store)
    {
        var episodeTitles = EpisodeTitles.ToLookup(title => title.OwnerID);
        var episodes = AnidbEpisodes.ToLookup(episode => episode.AnimeID);
        foreach (var anime in Anime)
        {
            var episodeEntries = episodes[anime.AnimeID]
                .Select(episode => (
                    ((IMetadata)episode).ID,
                    AnidbTextListing.PlanEpisodeTitles(
                        [],
                        episodeTitles[episode.EpisodeID].Select(title => new AnidbTextListing.ListedTitle(title.Language, title.Type, title.Value)),
                        episode.EpisodeType,
                        episode.EpisodeNumber
                    )
                ))
                .ToList();
            AnimeCreator.StoreTitles(store, ResponseTitlesFor(anime.AnimeID, variant: false), anime, episodeEntries, []);
        }
    }

    /// <summary>
    ///   Stores each TMDB entity's titles and overviews as the TMDB updater does: in the order listed, with the
    ///   American English ones equal to the English text on the entity's row left out and flagged as listed.
    /// </summary>
    /// <param name="store">The text store over the fixture's text cache.</param>
    private void SeedTmdbText(MetadataTextStore store)
    {
        var titles = TmdbTitles.ToLookup(t => (t.Type, t.ID));
        var overviews = TmdbOverviews.ToLookup(o => (o.Type, o.ID));
        var entries = new List<MetadataTextStore.ListedTexts>();
        foreach (var show in TmdbShows)
            entries.Add(Seed(show, MetadataEntityType.Series, show.TmdbShowID, titles, overviews));
        foreach (var episode in TmdbEpisodes)
            entries.Add(Seed(episode, MetadataEntityType.Episode, episode.TmdbEpisodeID, titles, overviews));
        foreach (var movie in TmdbMovies)
            entries.Add(Seed(movie, MetadataEntityType.Movie, movie.TmdbMovieID, titles, overviews));
        store.WriteListedTexts(entries);
    }

    private static MetadataTextStore.ListedTexts Seed(IEntityMetadata entity, MetadataEntityType type, int id, ILookup<(MetadataEntityType, int), TmdbText> titles,
        ILookup<(MetadataEntityType, int), TmdbText> overviews)
    {
        var (storedTitles, titleGap) = Listed(titles[(type, id)], entity.EnglishTitle, text => new TitleStub
        {
            Source = MetadataSource.TMDB,
            Language = text.Language.GetTitleLanguage(text.Country),
            LanguageCode = text.Language,
            CountryCode = text.Country,
            Value = text.Value,
            Type = TitleType.Official,
        });
        var (storedOverviews, overviewGap) = Listed(overviews[(type, id)], entity.EnglishOverview, text => (IText)new TextStub
        {
            Source = MetadataSource.TMDB,
            Language = text.Language.GetTitleLanguage(text.Country),
            LanguageCode = text.Language,
            CountryCode = text.Country,
            Value = text.Value,
        });
        entity.EnglishTitleListed = titleGap.HasValue;
        entity.EnglishOverviewListed = overviewGap.HasValue;
        return new(new(MetadataSource.TMDB, type, id.ToString()), storedTitles, titleGap, storedOverviews, overviewGap);
    }

    private static (List<T> Texts, int? Gap) Listed<T>(IEnumerable<TmdbText> texts, string? english, Func<TmdbText, T> build)
    {
        var stored = new List<T>();
        int? gap = null;
        foreach (var text in texts)
        {
            if (gap is null && text is { Language: "en", Country: "US" } && text.Value == english)
                gap = stored.Count;
            else
                stored.Add(build(text));
        }

        return (stored, gap);
    }

    /// <summary>
    ///   Builds a real cached repository over the given rows, with its own indexes, and no database behind it.
    /// </summary>
    /// <typeparam name="TRepo">The repository.</typeparam>
    /// <typeparam name="TEntity">The row.</typeparam>
    /// <param name="rows">The rows.</param>
    /// <param name="key">The row's primary key, as the repository selects it.</param>
    /// <returns>The repository.</returns>
    public static TRepo Build<TRepo, TEntity>(IEnumerable<TEntity> rows, Func<TEntity, int> key)
        where TRepo : BaseCachedRepository<TEntity, int>
        where TEntity : class, new()
    {
        var constructor = typeof(TRepo).GetConstructors().OrderBy(c => c.GetParameters().Length).First();
        var repository = (TRepo)constructor.Invoke(new object?[constructor.GetParameters().Length]);
        repository.Cache = new PocoCache<int, TEntity>(rows, key);
        repository.PopulateIndexes();
        return repository;
    }

    private static TRepo Set<TRepo>(TRepo repository) where TRepo : class
    {
        typeof(RepoFactory).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Single(field => field.FieldType == typeof(TRepo))
            .SetValue(null, repository);
        return repository;
    }

    #endregion

    #region Generating

    private void Generate(Random random)
    {
        int episodeID = 0, tmdbShowID = 100_000, tmdbEpisodeID = 1_000_000, tmdbMovieID = 500_000;
        int linkID = 0, groupID = 0;
        AnimeGroup? group = null;
        for (var animeID = 1; animeID <= AnimeCount; animeID++)
        {
            var type = PickType(random);
            var romaji = Romaji(random, random.Next(2, 5));
            var english = English(random, random.Next(2, 5));
            var japanese = Chars(random, Kanji, 2, 5) + Chars(random, Kana, 1, 4);
            var anime = new AniDB_Anime
            {
                AniDB_AnimeID = animeID,
                AnimeID = animeID,
                AnimeType = type,
                MainTitle = romaji,
                Description = random.NextDouble() < 0.82 ? Paragraph(random, 40, 140) : string.Empty,
            };

            var titles = new List<AnidbTitle>();
            void AnimeTitle(TitleType titleType, TitleLanguage language, string value)
                => titles.Add(new(anime.AnimeID, language, titleType, value));

            AnimeTitle(TitleType.Main, TitleLanguage.Romaji, romaji);
            AnimeTitle(TitleType.Official, TitleLanguage.Japanese, japanese);

            // A third of the anime have no English title on AniDB, so an English-first order reaches TMDB for them.
            if (random.NextDouble() < 0.67)
                AnimeTitle(TitleType.Official, TitleLanguage.English, english);
            if (random.NextDouble() < 0.3)
                AnimeTitle(TitleType.Official, TitleLanguage.German, "Die " + english);
            for (var synonyms = random.Next(0, 4); synonyms > 0; synonyms--)
                AnimeTitle(TitleType.Synonym, random.NextDouble() < 0.5 ? TitleLanguage.English : TitleLanguage.Romaji,
                    random.NextDouble() < 0.5 ? English(random, 2) : Romaji(random, 3));
            if (random.NextDouble() < 0.3)
                AnimeTitle(TitleType.Short, TitleLanguage.Romaji, Initials(romaji));
            Anime.Add(anime);
            AnimeTitles.AddRange(titles);

            // Every fourth group gathers three series, as a franchise does.
            if (group is null || Series.Count(s => s.AnimeGroupID == group.AnimeGroupID) >= (group.AnimeGroupID % 4 is 0 ? 3 : 1))
            {
                group = new() { AnimeGroupID = ++groupID, MainAniDBAnimeID = animeID };
                Groups.Add(group);
            }

            Series.Add(new() { AnimeSeriesID = animeID, AniDB_ID = animeID, AnimeGroupID = group.AnimeGroupID });

            var showID = 0;
            if (type is AnimeType.TVSeries or AnimeType.Web && random.NextDouble() < 0.6)
            {
                showID = ++tmdbShowID;
                var overview = Paragraph(random, 40, 120);
                TmdbShows.Add(new(showID) { EnglishTitle = english, OriginalTitle = japanese, OriginalLanguageCode = "ja", EnglishOverview = overview });
                AddTmdbText(random, MetadataEntityType.Series, showID, english, japanese, overview, 0.9);
                SeriesLinks.Add(new()
                {
                    CrossRef_AniDB_Metadata_SeriesID = ++linkID,
                    Source = MetadataSource.TMDB,
                    AnidbAnimeID = animeID,
                    ProviderID = $"{showID}",
                });
            }
            else if (type is AnimeType.Movie && random.NextDouble() < 0.8)
            {
                var overview = Paragraph(random, 40, 120);
                TmdbMovies.Add(new(++tmdbMovieID)
                {
                    EnglishTitle = english,
                    OriginalTitle = japanese,
                    OriginalLanguageCode = "ja",
                    EnglishOverview = overview,
                });
                AddTmdbText(random, MetadataEntityType.Movie, tmdbMovieID, english, japanese, overview, 0.9);
                MovieLinks.Add(new()
                {
                    CrossRef_AniDB_Metadata_MovieID = ++linkID,
                    Source = MetadataSource.TMDB,
                    AnidbAnimeID = animeID,
                    AnidbEpisodeID = episodeID + 1,
                    ProviderID = $"{tmdbMovieID}",
                });
            }

            var normal = type switch
            {
                AnimeType.Movie => 1,
                AnimeType.OVA => random.Next(1, 7),
                AnimeType.TVSpecial => random.Next(1, 3),
                _ => random.Next(10, 27),
            };
            var specials = type is AnimeType.Movie ? 0 : random.Next(0, 5);
            for (var number = 1; number <= normal + specials; number++)
            {
                var special = number > normal;
                var anidbEpisode = new AniDB_Episode
                {
                    AniDB_EpisodeID = ++episodeID,
                    EpisodeID = episodeID,
                    AnimeID = animeID,
                    EpisodeType = special ? EpisodeType.Special : EpisodeType.Episode,
                    EpisodeNumber = special ? number - normal : number,
                    Description = random.NextDouble() < 0.21 ? Paragraph(random, 20, 80) : string.Empty,
                };
                AnidbEpisodes.Add(anidbEpisode);
                Episodes.Add(new() { AnimeEpisodeID = episodeID, AnimeSeriesID = animeID, AniDB_EpisodeID = episodeID });

                void EpisodeTitle(TitleLanguage language, string value)
                    => EpisodeTitles.Add(new(anidbEpisode.EpisodeID, language, TitleType.None, value));

                var episodeEnglish = random.NextDouble() < 0.1 ? $"Episode {anidbEpisode.EpisodeNumber}" : English(random, random.Next(2, 6));
                EpisodeTitle(TitleLanguage.English, episodeEnglish);
                if (random.NextDouble() < 0.6)
                    EpisodeTitle(TitleLanguage.Romaji, Romaji(random, 4));
                if (random.NextDouble() < 0.5)
                    EpisodeTitle(TitleLanguage.Japanese, Chars(random, Kanji + Kana, 4, 12));

                if (showID is 0 || special)
                    continue;

                var episodeOverview = Paragraph(random, 20, 80);
                TmdbEpisodes.Add(new(++tmdbEpisodeID)
                {
                    TmdbShowID = showID,
                    TmdbSeasonID = showID * 10 + 1,
                    SeasonNumber = 1,
                    EpisodeNumber = number,
                    EnglishTitle = episodeEnglish,
                    EnglishOverview = episodeOverview,
                });
                AddTmdbText(random, MetadataEntityType.Episode, tmdbEpisodeID, episodeEnglish, Chars(random, Kanji + Kana, 4, 12), episodeOverview, 0.35);
                EpisodeLinks.Add(new()
                {
                    CrossRef_AniDB_Metadata_EpisodeID = ++linkID,
                    Source = MetadataSource.TMDB,
                    AnidbAnimeID = animeID,
                    AnidbEpisodeID = episodeID,
                    ProviderID = $"{tmdbEpisodeID}",
                    ProviderParentID = $"{showID}",
                    ProviderSeasonID = $"{showID * 10 + 1}",
                    SeasonNumber = 1,
                    EpisodeNumber = number,
                });
            }

            anime.EpisodeCountNormal = normal;
            anime.EpisodeCountSpecial = specials;
            anime.EpisodeCount = normal + specials;
        }
    }

    /// <summary>
    ///   Adds a TMDB entity's titles and overviews: English and the original always, the other languages each with
    ///   the given chance.
    /// </summary>
    private void AddTmdbText(Random random, MetadataEntityType type, int id, string english, string original, string overview, double chance)
    {
        foreach (var (language, country) in _tmdbTitleLanguages)
        {
            var value = language switch
            {
                "en" => english,
                "ja" => original,
                "fr" => "Le " + english,
                "de" => "Die " + english,
                "es" => "El " + english,
                "pt" => "O " + english,
                "ko" => Chars(random, Hangul, 3, 9),
                _ => Chars(random, Kanji, 3, 8),
            };
            if (language is "en" or "ja" || random.NextDouble() < chance)
                TmdbTitles.Add(new(type, id, value, language, country));
        }

        foreach (var (language, country) in _tmdbOverviewLanguages)
        {
            var value = language switch
            {
                "en" => overview,
                "ru" => string.Join(' ', Enumerable.Range(0, random.Next(20, 60)).Select(_ => Chars(random, Cyrillic, 2, 9))),
                _ => Paragraph(random, 20, 80),
            };
            if (language is "en" || random.NextDouble() < chance * 0.9)
                TmdbOverviews.Add(new(type, id, value, language, country));
        }
    }

    private void PickQueries(Random random)
    {
        AnidbTitle AnyTitle(TitleLanguage language)
        {
            var titles = AnimeTitles.Where(t => t.Language == language && t.Type is TitleType.Main or TitleType.Official).ToList();
            return titles[random.Next(titles.Count)];
        }

        var romaji = AnyTitle(TitleLanguage.Romaji).Value;
        var english = AnyTitle(TitleLanguage.English).Value;
        var typo = AnyTitle(TitleLanguage.Romaji).Value;
        typo = typo.Remove(typo.Length / 2, 1);
        Queries.AddRange(
        [
            english,
            romaji,
            english.Split(' ')[0],
            romaji[..Math.Min(romaji.Length, 6)],
            typo,
            AnyTitle(TitleLanguage.Japanese).Value[..2],
            _words[random.Next(_words.Length)].ToLowerInvariant(),
            AnyTitle(TitleLanguage.English).Value.ToUpperInvariant(),
            _syllables[random.Next(_syllables.Length)] + _syllables[random.Next(_syllables.Length)],
            "zzqx no match",
        ]);
    }

    private static AnimeType PickType(Random random)
        => random.NextDouble() switch
        {
            < 0.55 => AnimeType.TVSeries,
            < 0.70 => AnimeType.OVA,
            < 0.82 => AnimeType.Movie,
            < 0.92 => AnimeType.Web,
            < 0.97 => AnimeType.TVSpecial,
            _ => AnimeType.Other,
        };

    private static string Romaji(Random random, int words)
        => string.Join(' ', Enumerable.Range(0, words).Select(_ => Capitalize(Syllables(random, random.Next(2, 5)))));

    private static string Syllables(Random random, int count)
        => string.Concat(Enumerable.Range(0, count).Select(_ => _syllables[random.Next(_syllables.Length)]));

    private static string English(Random random, int words)
        => string.Join(' ', Enumerable.Range(0, words).Select(_ => _words[random.Next(_words.Length)]));

    private static string Paragraph(Random random, int min, int max)
        => string.Join(' ', Enumerable.Range(0, random.Next(min, max))
            .Select(_ => random.NextDouble() < 0.7 ? _words[random.Next(_words.Length)].ToLowerInvariant() : _syllables[random.Next(_syllables.Length)])) + ".";

    private static string Chars(Random random, string alphabet, int min, int max)
        => new([.. Enumerable.Range(0, random.Next(min, max)).Select(_ => alphabet[random.Next(alphabet.Length)])]);

    private static string Capitalize(string value)
        => char.ToUpperInvariant(value[0]) + value[1..];

    private static string Initials(string value)
        => string.Concat(value.Split(' ').Select(word => char.ToUpperInvariant(word[0])));

    #endregion

    #region Helpers

    /// <summary>
    ///   Fills every series' text memos, as the startup warm-up leaves them.
    /// </summary>
    public void WarmSeries()
    {
        foreach (var series in Series)
            _ = (series.PreferredTitle, series.PreferredOverview, series.Titles);
    }

    /// <summary>
    ///   The row counts, to turn the load timings into rows per second.
    /// </summary>
    /// <returns>One line per kind of row.</returns>
    public string Describe()
        => string.Join(Environment.NewLine,
        [
            $"anime: {Anime.Count}, anime titles: {AnimeTitles.Count}",
            $"episodes: {AnidbEpisodes.Count}, episode titles: {EpisodeTitles.Count}",
            $"series: {Series.Count}, groups: {Groups.Count}",
            $"tmdb shows: {TmdbShows.Count}, tmdb episodes: {TmdbEpisodes.Count}, tmdb movies: {TmdbMovies.Count}",
            $"tmdb titles: {TmdbTitles.Count}, tmdb overviews: {TmdbOverviews.Count}",
            $"links: series {SeriesLinks.Count}, movie {MovieLinks.Count}, episode {EpisodeLinks.Count}",
            $"queries: {string.Join(" | ", Queries)}",
            $"series titles chosen from: {BySource(Series.Select(s => s.PreferredTitle?.Source))}",
            $"series descriptions chosen from: {BySource(Series.Select(s => s.PreferredOverview?.Source))}",
            $"episode titles chosen from: {BySource(Episodes.Select(e => ((IWithTitles)e).PreferredTitle?.Source))}",
            $"episode descriptions chosen from: {BySource(Episodes.Select(e => ((IWithOverviews)e).PreferredOverview?.Source))}",
        ]);

    private static string BySource(IEnumerable<MetadataSource?> sources)
        => string.Join(", ", sources
            .GroupBy(source => source?.ToString() ?? "none")
            .OrderBy(group => group.Key)
            .Select(group => $"{group.Key} {group.Count()}"));

    #endregion

    #region Responses

    /// <summary>
    ///   The anime's titles as an AniDB HTTP response would list them, optionally with one of them renamed.
    /// </summary>
    /// <param name="animeID">The anime.</param>
    /// <param name="variant">Whether to rename one title, so a diff has one row to replace.</param>
    /// <returns>The response titles.</returns>
    public List<ResponseTitle> ResponseTitlesFor(int animeID, bool variant)
    {
        var titles = AnimeTitles
            .Where(t => t.OwnerID == animeID)
            .Select(t => new ResponseTitle { TitleType = t.Type, Language = t.Language, Title = t.Value })
            .ToList();
        if (variant && titles.FindLast(t => t.TitleType is not TitleType.Main) is { } renamed)
            renamed.Title = renamed.Title.EndsWith(" (alt)") ? renamed.Title[..^6] : renamed.Title + " (alt)";

        return titles;
    }

    #endregion

    #region AniDB Text

    /// <summary>
    ///   One title AniDB lists for an anime or an episode.
    /// </summary>
    /// <param name="OwnerID">The anime's or the episode's AniDB ID.</param>
    /// <param name="Language">The language.</param>
    /// <param name="Type">The kind of title; none for an episode's.</param>
    /// <param name="Value">The title.</param>
    public sealed record AnidbTitle(int OwnerID, TitleLanguage Language, TitleType Type, string Value);

    #endregion

    #region TMDB Text

    /// <summary>
    ///   One title or overview TMDB lists for an entity.
    /// </summary>
    /// <param name="Type">The kind of entity.</param>
    /// <param name="ID">The entity's TMDB ID.</param>
    /// <param name="Value">The text.</param>
    /// <param name="Language">The language code.</param>
    /// <param name="Country">The country code.</param>
    public sealed record TmdbText(MetadataEntityType Type, int ID, string Value, string Language, string Country);

    /// <summary>
    ///   Writes the text store's changes into its cache alone, handing new rows their IDs as a database would.
    /// </summary>
    private sealed class CacheOnlyWriter() : MetadataRowWriter(null!)
    {
        private int _nextID = 1;

        public override void Write(params IReadOnlyList<MetadataRowChanges> changes)
        {
            foreach (var change in changes)
                foreach (var row in change.Saving)
                    if (change.IDOf(row) is 0)
                        change.AssignID(row, _nextID++);

            foreach (var change in changes)
                change.Apply();
        }
    }

    #endregion
}
