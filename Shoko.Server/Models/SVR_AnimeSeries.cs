using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Commons.Extensions;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Server.Databases;
using Shoko.Server.Extensions;
using Shoko.Server.Repositories;
using Shoko.Server.Utilities;

using AbstractAnimeType = Shoko.Plugin.Abstractions.DataModels.AnimeType;
using AbstractEpisodeType = Shoko.Plugin.Abstractions.DataModels.EpisodeType;
using EpisodeType = Shoko.Models.Enums.EpisodeType;
using Range = System.Range;

namespace Shoko.Server.Models;

public class SVR_AnimeSeries : AnimeSeries, ISeries
{
    #region DB Columns

    public DateTime UpdatedAt { get; set; }

    public DataSourceType DisableAutoMatchFlags { get; set; } = 0;

    #endregion

    #region Disabled Auto Matching

    public bool IsTvDBAutoMatchingDisabled
    {
        get
        {
            return DisableAutoMatchFlags.HasFlag(DataSourceType.TvDB);
        }
        set
        {
            if (value)
                DisableAutoMatchFlags |= DataSourceType.TvDB;
            else
                DisableAutoMatchFlags &= ~DataSourceType.TvDB;
        }
    }

    public bool IsTMDBAutoMatchingDisabled
    {
        get
        {
            return DisableAutoMatchFlags.HasFlag(DataSourceType.TMDB);
        }
        set
        {
            if (value)
                DisableAutoMatchFlags |= DataSourceType.TMDB;
            else
                DisableAutoMatchFlags &= ~DataSourceType.TMDB;
        }
    }

    public bool IsTraktAutoMatchingDisabled
    {
        get
        {
            return DisableAutoMatchFlags.HasFlag(DataSourceType.Trakt);
        }
        set
        {
            if (value)
                DisableAutoMatchFlags |= DataSourceType.Trakt;
            else
                DisableAutoMatchFlags &= ~DataSourceType.Trakt;
        }
    }

    public bool IsMALAutoMatchingDisabled
    {
        get
        {
            return DisableAutoMatchFlags.HasFlag(DataSourceType.MAL);
        }
        set
        {
            if (value)
                DisableAutoMatchFlags |= DataSourceType.MAL;
            else
                DisableAutoMatchFlags &= ~DataSourceType.MAL;
        }
    }

    public bool IsAniListAutoMatchingDisabled
    {
        get
        {
            return DisableAutoMatchFlags.HasFlag(DataSourceType.AniList);
        }
        set
        {
            if (value)
                DisableAutoMatchFlags |= DataSourceType.AniList;
            else
                DisableAutoMatchFlags &= ~DataSourceType.AniList;
        }
    }

    public bool IsAnimeshonAutoMatchingDisabled
    {
        get
        {
            return DisableAutoMatchFlags.HasFlag(DataSourceType.Animeshon);
        }
        set
        {
            if (value)
                DisableAutoMatchFlags |= DataSourceType.Animeshon;
            else
                DisableAutoMatchFlags &= ~DataSourceType.Animeshon;
        }
    }

    public bool IsKitsuAutoMatchingDisabled
    {
        get
        {
            return DisableAutoMatchFlags.HasFlag(DataSourceType.Kitsu);
        }
        set
        {
            if (value)
                DisableAutoMatchFlags |= DataSourceType.Kitsu;
            else
                DisableAutoMatchFlags &= ~DataSourceType.Kitsu;
        }
    }

    #endregion

    public string SeriesName
    {
        get
        {
            // Return the override if it's set.
            if (!string.IsNullOrEmpty(SeriesNameOverride))
                return SeriesNameOverride;

            if (Utils.SettingsProvider.GetSettings().SeriesNameSource == DataSourceType.AniDB)
                return AniDB_Anime.PreferredTitle;

            // Try to find the TvDB title if we prefer TvDB titles.
            var tvdbShows = TvDBSeries;
            var tvdbShowTitle = tvdbShows
                .FirstOrDefault(show =>
                    !show.SeriesName.Contains("**DUPLICATE", StringComparison.InvariantCultureIgnoreCase))?.SeriesName;
            if (!string.IsNullOrEmpty(tvdbShowTitle))
                return tvdbShowTitle;

            // Otherwise just return the anidb title.
            return AniDB_Anime.PreferredTitle;
        }
    }

    public List<SVR_VideoLocal> VideoLocals => RepoFactory.VideoLocal.GetByAniDBAnimeID(AniDB_ID);

    public IReadOnlyList<SVR_AnimeEpisode> AnimeEpisodes => RepoFactory.AnimeEpisode.GetBySeriesID(AnimeSeriesID)
        .Where(episode => !episode.IsHidden)
        .Select(episode => (episode, anidbEpisode: episode.AniDB_Episode))
        .OrderBy(tuple => tuple.anidbEpisode.EpisodeType)
        .ThenBy(tuple => tuple.anidbEpisode.EpisodeNumber)
        .Select(tuple => tuple.episode)
        .ToList();

    public IReadOnlyList<SVR_AnimeEpisode> AllAnimeEpisodes => RepoFactory.AnimeEpisode.GetBySeriesID(AnimeSeriesID)
        .Select(episode => (episode, anidbEpisode: episode.AniDB_Episode))
        .OrderBy(tuple => tuple.anidbEpisode.EpisodeType)
        .ThenBy(tuple => tuple.anidbEpisode.EpisodeNumber)
        .Select(tuple => tuple.episode)
        .ToList();

    public MovieDB_Movie MovieDB_Movie
    {
        get
        {
            var movieDBXRef = RepoFactory.CrossRef_AniDB_Other.GetByAnimeIDAndType(AniDB_ID, CrossRefType.MovieDB);
            if (movieDBXRef?.CrossRefID == null || !int.TryParse(movieDBXRef.CrossRefID, out var movieID))
            {
                return null;
            }

            var movieDB = RepoFactory.MovieDb_Movie.GetByOnlineID(movieID);
            return movieDB;
        }
    }


    #region TvDB

    public List<CrossRef_AniDB_TvDB> TvDBXrefs => RepoFactory.CrossRef_AniDB_TvDB.GetByAnimeID(AniDB_ID);

    public List<TvDB_Series> TvDBSeries
    {
        get
        {
            var xrefs = TvDBXrefs?.WhereNotNull().ToArray();
            if (xrefs == null || xrefs.Length == 0) return [];
            return xrefs.Select(xref => xref.GetTvDBSeries()).WhereNotNull().ToList();
        }
    }

    #endregion

    public List<Trakt_Show> TraktShow
    {
        get
        {
            using var session = Utils.ServiceContainer.GetRequiredService<DatabaseFactory>().SessionFactory.OpenSession();
            var sers = new List<Trakt_Show>();

            var xrefs = RepoFactory.CrossRef_AniDB_TraktV2.GetByAnimeID(AniDB_ID);
            if (xrefs == null || xrefs.Count == 0)
            {
                return sers;
            }

            foreach (var xref in xrefs)
            {
                sers.Add(xref.GetByTraktShow(session));
            }

            return sers;
        }
    }

    public CrossRef_AniDB_Other CrossRefMovieDB =>
        RepoFactory.CrossRef_AniDB_Other.GetByAnimeIDAndType(AniDB_ID, CrossRefType.MovieDB);

    public List<CrossRef_AniDB_MAL> CrossRefMAL => RepoFactory.CrossRef_AniDB_MAL.GetByAnimeID(AniDB_ID);

    public SVR_AniDB_Anime AniDB_Anime => RepoFactory.AniDB_Anime.GetByAnimeID(AniDB_ID);

    private DateTime? _airDate;
    public IReadOnlyList<int> GroupIDs => AllGroupsAbove.Select(a => a.AnimeGroupID).Distinct().ToList();

    public DateTime? AirDate
    {
        get
        {
            if (_airDate != null) return _airDate;
            var anime = AniDB_Anime;
            if (anime?.AirDate != null)
                return _airDate = anime.AirDate.Value;

            // This will be slower, but hopefully more accurate
            var ep = RepoFactory.AniDB_Episode.GetByAnimeID(AniDB_ID)
                .Where(a => a.EpisodeType == (int)EpisodeType.Episode && a.LengthSeconds > 0 && a.AirDate != 0)
                .MinBy(a => a.AirDate);
            return _airDate = ep?.GetAirDateAsDate();
        }
    }

    private DateTime? _endDate;
    public DateTime? EndDate
    {
        get
        {
            if (_endDate != null) return _endDate;
            return _endDate = AniDB_Anime?.EndDate;
        }
    }

    public HashSet<int> Years
    {
        get
        {
            var anime = AniDB_Anime;
            var startyear = anime?.BeginYear ?? 0;
            if (startyear == 0) return [];

            var endyear = anime?.EndYear ?? 0;
            if (endyear == 0) endyear = DateTime.Today.Year;
            if (endyear < startyear) endyear = startyear;
            if (startyear == endyear) return [startyear];

            return Enumerable.Range(startyear, endyear - startyear + 1).Where(anime.IsInYear).ToHashSet();
        }
    }

    /// <summary>
    /// Gets the direct parent AnimeGroup this series belongs to
    /// </summary>
    public SVR_AnimeGroup AnimeGroup => RepoFactory.AnimeGroup.GetByID(AnimeGroupID);

    /// <summary>
    /// Gets the very top level AnimeGroup which this series belongs to
    /// </summary>
    public SVR_AnimeGroup TopLevelAnimeGroup
    {
        get
        {
            var parentGroup = RepoFactory.AnimeGroup.GetByID(AnimeGroupID);

            int parentID;
            while ((parentID = parentGroup?.AnimeGroupParentID ?? 0) != 0)
            {
                parentGroup = RepoFactory.AnimeGroup.GetByID(parentID);
            }

            return parentGroup;
        }
    }

    public List<SVR_AnimeGroup> AllGroupsAbove
    {
        get
        {
            var grps = new List<SVR_AnimeGroup>();
            var groupID = AnimeGroupID;
            while (groupID != 0)
            {
                var grp = RepoFactory.AnimeGroup.GetByID(groupID);
                if (grp != null)
                {
                    grps.Add(grp);
                    groupID = grp.AnimeGroupParentID ?? 0;
                }
                else
                {
                    groupID = 0;
                }
            }

            return grps;
        }
    }

    public override string ToString()
    {
        return $"Series: {AniDB_Anime.MainTitle} ({AnimeSeriesID})";
        //return string.Empty;
    }

    #region IMetadata Implementation

    DataSourceEnum IMetadata.Source => DataSourceEnum.Shoko;

    int IMetadata<int>.ID => AnimeSeriesID;

    #endregion

    #region IWithTitles Implementation

    string IWithTitles.DefaultTitle => SeriesNameOverride ?? AniDB_Anime?.MainTitle ?? $"<Shoko Series {AnimeSeriesID}>";

    string IWithTitles.PreferredTitle => SeriesName;

    IReadOnlyList<AnimeTitle> IWithTitles.Titles
    {
        get
        {
            var titles = new List<AnimeTitle>();
            var seriesOverrideTitle = false;
            if (!string.IsNullOrEmpty(SeriesNameOverride))
            {
                titles.Add(new()
                {
                    Source = DataSourceEnum.Shoko,
                    Language = TitleLanguage.Unknown,
                    LanguageCode = "unk",
                    Title = SeriesNameOverride,
                    Type = TitleType.Main,
                });
                seriesOverrideTitle = true;
            }

            var anime = AniDB_Anime;
            if (anime is not null && anime is ISeries animeSeries)
            {
                var animeTitles = animeSeries.Titles;
                if (seriesOverrideTitle)
                {
                    var mainTitle = animeTitles.FirstOrDefault(title => title.Type == TitleType.Main);
                    if (mainTitle is not null)
                        mainTitle.Type = TitleType.Official;
                }
                titles.AddRange(animeTitles);
            }

            // TODO: Add other sources here.

            return titles;
        }
    }

    #endregion

    #region ISeries Implementation

    AbstractAnimeType ISeries.Type => (AbstractAnimeType)(AniDB_Anime?.AnimeType ?? -1);
    public int? SeriesID => AnimeSeriesID;

    double ISeries.Rating => (AniDB_Anime?.Rating ?? 0) / 100D;

    bool ISeries.Restricted => (AniDB_Anime?.Restricted ?? 0) == 1;

    IReadOnlyList<ISeries> ISeries.LinkedSeries
    {
        get
        {
            var seriesList = new List<ISeries>();

            var anidbAnime = RepoFactory.AniDB_Anime.GetByAnimeID(AniDB_ID);
            if (anidbAnime is not null)
                seriesList.Add(anidbAnime);

            // TODO: Add more series here.

            return seriesList;
        }
    }

    IReadOnlyList<IRelatedMetadata<ISeries>> ISeries.RelatedSeries =>
        RepoFactory.AniDB_Anime_Relation.GetByAnimeID(AniDB_ID);

    IReadOnlyList<IVideoCrossReference> ISeries.CrossReferences =>
        RepoFactory.CrossRef_File_Episode.GetByAnimeID(AniDB_ID);

    IReadOnlyList<IEpisode> ISeries.EpisodeList => AllAnimeEpisodes;

    IReadOnlyList<IVideo> ISeries.VideoList =>
        RepoFactory.CrossRef_File_Episode.GetByAnimeID(AniDB_ID)
            .DistinctBy(xref => xref.Hash)
            .Select(xref => xref.GetVideo())
            .WhereNotNull()
            .ToList();

    IReadOnlyDictionary<AbstractEpisodeType, int> ISeries.EpisodeCountDict
    {
        get
        {
            var episodes = (this as ISeries).EpisodeList;
            return Enum.GetValues<AbstractEpisodeType>()
                .ToDictionary(a => a, a => episodes.Count(e => e.Type == a));
        }
    }

    #endregion
}
