﻿using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Commons.Extensions;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Server.Repositories;
using Shoko.Server.Utilities;

using AbstractEpisodeType = Shoko.Plugin.Abstractions.DataModels.EpisodeType;
using AnimeTitle = Shoko.Plugin.Abstractions.DataModels.AnimeTitle;
using EpisodeType = Shoko.Models.Enums.EpisodeType;

#nullable enable
namespace Shoko.Server.Models;

public class SVR_AnimeEpisode : AnimeEpisode, IEpisode
{
    #region DB Columns

    /// <summary>
    /// Episode name override.
    /// </summary>
    /// <value></value>
    public string? EpisodeNameOverride { get; set; }

    #endregion

    public EpisodeType EpisodeTypeEnum => (EpisodeType)(AniDB_Episode?.EpisodeType ?? 1);

    public SVR_AniDB_Episode? AniDB_Episode => RepoFactory.AniDB_Episode.GetByEpisodeID(AniDB_EpisodeID);

    public SVR_AnimeEpisode_User? GetUserRecord(int userID)
    {
        return RepoFactory.AnimeEpisode_User.GetByUserIDAndEpisodeID(userID, AnimeEpisodeID);
    }

    /// <summary>
    /// Gets the AnimeSeries this episode belongs to
    /// </summary>
    public SVR_AnimeSeries? AnimeSeries => RepoFactory.AnimeSeries.GetByID(AnimeSeriesID);

    public List<SVR_VideoLocal> VideoLocals => RepoFactory.VideoLocal.GetByAniDBEpisodeID(AniDB_EpisodeID);

    public List<SVR_CrossRef_File_Episode> FileCrossRefs => RepoFactory.CrossRef_File_Episode.GetByEpisodeID(AniDB_EpisodeID);

    public TvDB_Episode? TvDBEpisode
    {
        get
        {
            // Try Overrides first, then regular
            return RepoFactory.CrossRef_AniDB_TvDB_Episode_Override.GetByAniDBEpisodeID(AniDB_EpisodeID)
                .Select(a => RepoFactory.TvDB_Episode.GetByTvDBID(a.TvDBEpisodeID)).Where(a => a != null)
                .OrderBy(a => a.SeasonNumber).ThenBy(a => a.EpisodeNumber).FirstOrDefault() ?? RepoFactory.CrossRef_AniDB_TvDB_Episode.GetByAniDBEpisodeID(AniDB_EpisodeID)
                .Select(a => RepoFactory.TvDB_Episode.GetByTvDBID(a.TvDBEpisodeID)).Where(a => a != null)
                .OrderBy(a => a.SeasonNumber).ThenBy(a => a.EpisodeNumber).FirstOrDefault();
        }
    }

    public List<TvDB_Episode> TvDBEpisodes
    {
        get
        {
            // Try Overrides first, then regular
            var overrides = RepoFactory.CrossRef_AniDB_TvDB_Episode_Override.GetByAniDBEpisodeID(AniDB_EpisodeID)
                .Select(a => RepoFactory.TvDB_Episode.GetByTvDBID(a.TvDBEpisodeID)).Where(a => a != null)
                .OrderBy(a => a.SeasonNumber).ThenBy(a => a.EpisodeNumber).ToList();
            return overrides.Count > 0
                ? overrides
                : RepoFactory.CrossRef_AniDB_TvDB_Episode.GetByAniDBEpisodeID(AniDB_EpisodeID)
                    .Select(a => RepoFactory.TvDB_Episode.GetByTvDBID(a.TvDBEpisodeID)).Where(a => a != null)
                    .OrderBy(a => a.SeasonNumber).ThenBy(a => a.EpisodeNumber).ToList();
        }
    }

    public double UserRating
    {
        get
        {
            AniDB_Vote vote = RepoFactory.AniDB_Vote.GetByEntityAndType(AnimeEpisodeID, AniDBVoteType.Episode);
            if (vote != null) return vote.VoteValue / 100D;
            return -1;
        }
    }

    public string DefaultTitle
    {
        get
        {
            // Fallback to English if available.
            return RepoFactory.AniDB_Episode_Title.GetByEpisodeIDAndLanguage(AniDB_EpisodeID, TitleLanguage.English)
                .FirstOrDefault()
                ?.Title ?? $"Shoko Episode {AnimeEpisodeID}";
        }
    }

    public string PreferredTitle
    {
        get
        {
            if (!string.IsNullOrEmpty(EpisodeNameOverride))
                return EpisodeNameOverride;

            // Try finding one of the preferred languages.
            foreach (var language in Languages.PreferredEpisodeNamingLanguages)
            {
                var title = RepoFactory.AniDB_Episode_Title.GetByEpisodeIDAndLanguage(AniDB_EpisodeID, language.Language)
                    .FirstOrDefault()
                    ?.Title;
                if (!string.IsNullOrEmpty(title))
                    return title;
            }

            return DefaultTitle;
        }
    }

    protected bool Equals(SVR_AnimeEpisode other)
    {
        return AnimeEpisodeID == other.AnimeEpisodeID && AnimeSeriesID == other.AnimeSeriesID &&
               AniDB_EpisodeID == other.AniDB_EpisodeID && DateTimeUpdated.Equals(other.DateTimeUpdated) &&
               DateTimeCreated.Equals(other.DateTimeCreated);
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj))
        {
            return false;
        }

        if (ReferenceEquals(this, obj))
        {
            return true;
        }

        if (obj.GetType() != this.GetType())
        {
            return false;
        }

        return Equals((SVR_AnimeEpisode)obj);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = AnimeEpisodeID;
            hashCode = (hashCode * 397) ^ AnimeSeriesID;
            hashCode = (hashCode * 397) ^ AniDB_EpisodeID;
            hashCode = (hashCode * 397) ^ DateTimeUpdated.GetHashCode();
            hashCode = (hashCode * 397) ^ DateTimeCreated.GetHashCode();
            return hashCode;
        }
    }
    #region IMetadata Implementation

    DataSourceEnum IMetadata.Source => DataSourceEnum.Shoko;

    int IMetadata<int>.ID => AnimeEpisodeID;

    #endregion

    #region IWithTitles Implementation

    string IWithTitles.DefaultTitle => DefaultTitle;

    string IWithTitles.PreferredTitle => PreferredTitle;

    IReadOnlyList<AnimeTitle> IWithTitles.Titles
    {
        get
        {
            var titles = new List<AnimeTitle>();
            var episodeOverrideTitle = false;
            if (!string.IsNullOrEmpty(EpisodeNameOverride))
            {
                titles.Add(new()
                {
                    Source = DataSourceEnum.Shoko,
                    Language = TitleLanguage.Unknown,
                    LanguageCode = "unk",
                    Title = EpisodeNameOverride,
                    Type = TitleType.Main,
                });
                episodeOverrideTitle = true;
            }

            var episode = AniDB_Episode;
            if (episode is not null && episode is IEpisode animeEpisode)
            {
                var animeTitles = animeEpisode.Titles;
                if (episodeOverrideTitle)
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

    #region IWithDescription Implementation

    string IWithDescriptions.DefaultDescription => AniDB_Episode?.Description ?? string.Empty;

    string IWithDescriptions.PreferredDescription => AniDB_Episode?.Description ?? string.Empty;

    IReadOnlyList<TextDescription> IWithDescriptions.Descriptions
    {
        get
        {
            var titles = new List<TextDescription>();

            var episode = AniDB_Episode;
            if (episode is not null && episode is IEpisode anidbEpisode)
            {
                var episodetitles = anidbEpisode.Descriptions;
                titles.AddRange(episodetitles);
            }

            // TODO: Add other sources here.

            // Fallback in the off-chance that the AniDB data is unavailable for whatever reason. It should never reach this code.
            if (titles.Count is 0)
                titles.Add(new()
                {
                    Source = DataSourceEnum.AniDB,
                    Language = TitleLanguage.English,
                    LanguageCode = "en",
                    Value = string.Empty,
                });

            return titles;
        }
    }

    #endregion

    #region IEpisode Implementation

    int IEpisode.SeriesID => AnimeSeriesID;

    AbstractEpisodeType IEpisode.Type => (AbstractEpisodeType)EpisodeTypeEnum;

    int IEpisode.EpisodeNumber => AniDB_Episode?.EpisodeNumber ?? 1;

    int? IEpisode.SeasonNumber => EpisodeTypeEnum == EpisodeType.Episode ? 1 : null;

    TimeSpan IEpisode.Runtime => TimeSpan.FromSeconds(AniDB_Episode?.LengthSeconds ?? 0);

    DateTime? IEpisode.AirDate => AniDB_Episode?.GetAirDateAsDate();

    ISeries? IEpisode.SeriesInfo => AnimeSeries;

    IReadOnlyList<IEpisode> IEpisode.LinkedEpisodes
    {
        get
        {
            var episodeList = new List<IEpisode>();

            var anidbEpisode = AniDB_Episode;
            if (anidbEpisode is not null)
                episodeList.Add(anidbEpisode);

            // TODO: Add more episodes here.

            return episodeList;
        }
    }

    IReadOnlyList<IVideoCrossReference> IEpisode.CrossReferences =>
        RepoFactory.CrossRef_File_Episode.GetByEpisodeID(AniDB_EpisodeID);

    IReadOnlyList<IVideo> IEpisode.VideoList =>
        RepoFactory.CrossRef_File_Episode.GetByEpisodeID(AniDB_EpisodeID)
            .DistinctBy(xref => xref.Hash)
            .Select(xref => xref.VideoLocal)
            .OfType<SVR_VideoLocal>()
            .ToList();

    int IEpisode.EpisodeID => AnimeEpisodeID;

    int IEpisode.AnimeID => AnimeSeriesID;

    int IEpisode.Number => AniDB_Episode?.EpisodeNumber ?? 1;

    int IEpisode.Duration => AniDB_Episode?.LengthSeconds ?? 0;

    #endregion
}
