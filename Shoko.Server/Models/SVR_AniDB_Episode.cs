using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Commons.Extensions;
using Shoko.Models.Server;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Server.Repositories;
using Shoko.Server.Utilities;

using EpisodeTypeEnum = Shoko.Models.Enums.EpisodeType;

#nullable enable
namespace Shoko.Server.Models;

public class SVR_AniDB_Episode : AniDB_Episode, IEpisode
{
    public EpisodeTypeEnum EpisodeTypeEnum => (EpisodeTypeEnum)EpisodeType;

    public TimeSpan Runtime => TimeSpan.FromSeconds(LengthSeconds);

    public string GetDefaultTitle() =>
        RepoFactory.AniDB_Episode_Title.GetByEpisodeIDAndLanguage(EpisodeID, TitleLanguage.English)
            .FirstOrDefault()
            ?.Title ?? $"Episode {EpisodeNumber}";

    public string GetPreferredTitle()
    {
        // Try finding one of the preferred languages.
        foreach (var language in Languages.PreferredEpisodeNamingLanguages)
        {
            var title = RepoFactory.AniDB_Episode_Title.GetByEpisodeIDAndLanguage(EpisodeID, language.Language)
                .FirstOrDefault()
                ?.Title;
            if (!string.IsNullOrEmpty(title))
                return title;
        }

        // Fallback to English if available.
        return GetDefaultTitle();
    }

    public IReadOnlyList<SVR_AniDB_Episode_Title> GetTitles(TitleLanguage? language = null) => language.HasValue
        ? RepoFactory.AniDB_Episode_Title.GetByEpisodeIDAndLanguage(EpisodeID, language.Value)
        : RepoFactory.AniDB_Episode_Title.GetByEpisodeID(EpisodeID);

    public SVR_AniDB_Anime? GetAnime() =>
        RepoFactory.AniDB_Anime.GetByAnimeID(AnimeID);

    public SVR_AnimeEpisode? GetShokoEpisode() =>
        RepoFactory.AnimeEpisode.GetByAniDBEpisodeID(EpisodeID);

    public SVR_AnimeSeries? GetShokoSeries() =>
        RepoFactory.AnimeSeries.GetByAnimeID(AnimeID);

    #region IMetadata Implementation

    DataSourceEnum IMetadata.Source => DataSourceEnum.AniDB;

    int IMetadata<int>.ID => EpisodeID;

    #endregion

    #region IWithTitles Implementation

    string IWithTitles.DefaultTitle => GetDefaultTitle();

    string IWithTitles.PreferredTitle => GetPreferredTitle();

    IReadOnlyList<AnimeTitle> IWithTitles.Titles
    {
        get
        {
            var defaultTitle = GetDefaultTitle();
            return GetTitles()
                .Select(a => new AnimeTitle
                {
                    Source = DataSourceEnum.AniDB,
                    LanguageCode = a.LanguageCode,
                    Language = a.Language,
                    Title = a.Title,
                    Type = string.Equals(a.Title, defaultTitle) ? TitleType.Main : TitleType.None,
                })
                .ToList();
        }
    }

    #endregion

    #region IEpisode Implementation

    int IEpisode.SeriesID => AnimeID;

    EpisodeType IEpisode.Type => (EpisodeType)EpisodeType;

    int? IEpisode.SeasonNumber => EpisodeType == (int)EpisodeTypeEnum.Episode ? 1 : null;

    DateTime? IEpisode.AirDate => this.GetAirDateAsDate();

    ISeries? IEpisode.SeriesInfo => GetAnime();

    IReadOnlyList<IEpisode> IEpisode.LinkedEpisodes
    {
        get
        {
            var episodeList = new List<IEpisode>();

            var shokoEpisode = GetShokoEpisode();
            if (shokoEpisode is not null)
                episodeList.Add(shokoEpisode);

            // TODO: Add more episodes here.

            return episodeList;
        }
    }

    IReadOnlyList<IVideoCrossReference> IEpisode.CrossReferences =>
        RepoFactory.CrossRef_File_Episode.GetByEpisodeID(EpisodeID);

    IReadOnlyList<IVideo> IEpisode.VideoList =>
        RepoFactory.CrossRef_File_Episode.GetByEpisodeID(EpisodeID)
            .DistinctBy(xref => xref.Hash)
            .Select(xref => xref.GetVideo())
            .OfType<SVR_VideoLocal>()
            .ToList();

    int IEpisode.EpisodeID => EpisodeID;

    int IEpisode.AnimeID => AnimeID;

    int IEpisode.Number => EpisodeNumber;

    int IEpisode.Duration => LengthSeconds;

    #endregion
}
