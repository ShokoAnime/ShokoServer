﻿using System;
using System.Globalization;
using NLog;
using Shoko.Models.Enums;
using Shoko.Models.Metro;
using Shoko.Models.Server;
using Shoko.Models.TvDB;
using Shoko.Server.Models;
using Shoko.Server.Providers.TraktTV.Contracts;
using Shoko.Server.Repositories;
using TvDbSharper.Dto;

namespace Shoko.Server.Extensions;

public static class ModelProviders
{
    private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

    public static void Populate(this Trakt_Show show, TraktV2ShowExtended tvShow)
    {
        show.Overview = tvShow.overview;
        show.Title = tvShow.title;
        show.TraktID = tvShow.ids.slug;
        show.TvDB_ID = tvShow.ids.tvdb;
        show.URL = tvShow.ShowURL;
        show.Year = tvShow.year.ToString();
    }

    public static void Populate(this TvDB_Episode episode, EpisodeRecord apiEpisode)
    {
        episode.Id = apiEpisode.Id;
        episode.SeriesID = apiEpisode.SeriesId;
        episode.SeasonID = 0;
        episode.SeasonNumber = apiEpisode.AiredSeason ?? 0;
        episode.EpisodeNumber = apiEpisode.AiredEpisodeNumber ?? 0;

        var flag = 0;
        if (apiEpisode.Filename != string.Empty)
        {
            flag = 1;
        }

        episode.EpImgFlag = flag;
        episode.AbsoluteNumber = apiEpisode.AbsoluteNumber ?? 0;
        episode.EpisodeName = apiEpisode.EpisodeName ?? string.Empty;
        episode.Overview = apiEpisode.Overview;
        episode.Filename = apiEpisode.Filename ?? string.Empty;
        episode.AirsAfterSeason = apiEpisode.AirsAfterSeason;
        episode.AirsBeforeEpisode = apiEpisode.AirsBeforeEpisode;
        episode.AirsBeforeSeason = apiEpisode.AirsBeforeSeason;
        if (apiEpisode.SiteRating != null)
        {
            episode.Rating = (int)Math.Round(apiEpisode.SiteRating.Value);
        }

        if (!string.IsNullOrEmpty(apiEpisode.FirstAired))
        {
            episode.AirDate =
                DateTime.ParseExact(apiEpisode.FirstAired, "yyyy-MM-dd", DateTimeFormatInfo.InvariantInfo);
        }
    }

    public static bool Populate(this TvDB_ImageFanart fanart, int seriesID, Image image)
    {
        try
        {
            fanart.SeriesID = seriesID;
            fanart.Id = image.Id;
            fanart.BannerPath = image.FileName;
            fanart.BannerType2 = image.Resolution;
            fanart.Colors = string.Empty;
            fanart.VignettePath = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in TvDB_ImageFanart.Init: " + ex);
            return false;
        }
    }

    public static bool Populate(this TvDB_ImagePoster poster, int seriesID, Image image)
    {
        try
        {
            poster.SeriesID = seriesID;
            poster.SeasonNumber = null;
            poster.Id = image.Id;
            poster.BannerPath = image.FileName;
            poster.BannerType = image.KeyType;
            poster.BannerType2 = image.Resolution;
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in TvDB_ImagePoster.Populate: " + ex);
            return false;
        }
    }

    public static bool Populate(this TvDB_ImageWideBanner poster, int seriesID, Image image)
    {
        try
        {
            poster.SeriesID = seriesID;
            try
            {
                poster.SeasonNumber = int.Parse(image.SubKey);
            }
            catch (FormatException)
            {
                poster.SeasonNumber = null;
            }

            poster.Id = image.Id;
            poster.BannerPath = image.FileName;
            poster.BannerType = image.KeyType;
            poster.BannerType2 = image.Resolution;
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in TvDB_ImageWideBanner.Populate: " + ex);
            return false;
        }
    }

    public static void PopulateFromSeriesInfo(this TvDB_Series series, Series apiSeries)
    {
        series.SeriesID = 0;
        series.Overview = string.Empty;
        series.SeriesName = string.Empty;
        series.Status = string.Empty;
        series.Banner = string.Empty;
        series.Fanart = string.Empty;
        series.Lastupdated = string.Empty;
        series.Poster = string.Empty;

        series.SeriesID = apiSeries.Id;
        series.SeriesName = apiSeries.SeriesName;
        series.Overview = apiSeries.Overview;
        series.Banner = apiSeries.Banner;
        series.Status = apiSeries.Status;
        series.Lastupdated = apiSeries.LastUpdated.ToString();
        if (apiSeries.SiteRating != null)
        {
            series.Rating = (int)Math.Round(apiSeries.SiteRating.Value * 10);
        }
    }

    public static void Populate(this TVDB_Series_Search_Response response, SeriesSearchResult series)
    {
        response.Id = string.Empty;
        response.SeriesID = series.Id;
        response.SeriesName = series.SeriesName;
        response.Overview = series.Overview;
        response.Banner = series.Banner;
        response.Language = string.Intern("en");
    }

    public static Metro_AniDB_Character ToContractMetro(this AniDB_Character character,
        AniDB_Anime_Character charRel)
    {
        var contract = new Metro_AniDB_Character
        {
            AniDB_CharacterID = character.AniDB_CharacterID,
            CharID = character.CharID,
            CharName = character.CharName,
            CharKanjiName = character.CharKanjiName,
            CharDescription = character.CharDescription,
            CharType = charRel.CharType,
            ImageType = (int)CL_ImageEntityType.AniDB_Character,
            ImageID = character.AniDB_CharacterID
        };
        var seiyuu = character.GetSeiyuu();
        if (seiyuu != null)
        {
            contract.SeiyuuID = seiyuu.AniDB_SeiyuuID;
            contract.SeiyuuName = seiyuu.SeiyuuName;
            contract.SeiyuuImageType = (int)CL_ImageEntityType.AniDB_Creator;
            contract.SeiyuuImageID = seiyuu.AniDB_SeiyuuID;
        }

        return contract;
    }

    public static void Populate(this SVR_AnimeGroup group, SVR_AnimeSeries series)
    {
        group.Populate(series, DateTime.Now);
    }

    public static void Populate(this SVR_AnimeGroup group, SVR_AnimeSeries series, DateTime now)
    {
        var anime = series.AniDB_Anime;

        group.Description = anime.Description;
        var name = series.PreferredTitle;
        group.GroupName = name;
        group.MainAniDBAnimeID = series.AniDB_ID;
        group.DateTimeUpdated = now;
        group.DateTimeCreated = now;
    }

    public static void Populate(this SVR_AnimeGroup group, SVR_AniDB_Anime anime, DateTime now)
    {
        group.Description = anime.Description;
        var name = anime.PreferredTitle;
        group.GroupName = name;
        group.MainAniDBAnimeID = anime.AnimeID;
        group.DateTimeUpdated = now;
        group.DateTimeCreated = now;
    }

    public static void Populate(this SVR_AnimeEpisode episode, SVR_AniDB_Episode anidbEpisode)
    {
        episode.AniDB_EpisodeID = anidbEpisode.EpisodeID;
        episode.DateTimeUpdated = DateTime.Now;
        episode.DateTimeCreated = DateTime.Now;
    }

    public static (int season, int episodeNumber) GetNextEpisode(this TvDB_Episode ep)
    {
        if (ep == null)
        {
            return (0, 0);
        }

        var epsInSeason = RepoFactory.TvDB_Episode.GetNumberOfEpisodesForSeason(ep.SeriesID, ep.SeasonNumber);
        if (ep.EpisodeNumber == epsInSeason)
        {
            var numberOfSeasons = RepoFactory.TvDB_Episode.GetLastSeasonForSeries(ep.SeriesID);
            if (ep.SeasonNumber == numberOfSeasons)
            {
                return (0, 0);
            }

            return (ep.SeasonNumber + 1, 1);
        }

        return (ep.SeasonNumber, ep.EpisodeNumber + 1);
    }

    public static (int season, int episodeNumber) GetPreviousEpisode(this TvDB_Episode ep)
    {
        // check bounds and exit
        if (ep.SeasonNumber == 1 && ep.EpisodeNumber == 1)
        {
            return (0, 0);
        }

        // self explanatory
        if (ep.EpisodeNumber > 1)
        {
            return (ep.SeasonNumber, ep.EpisodeNumber - 1);
        }

        // episode number is 1
        // get the last episode of last season
        var epsInSeason = RepoFactory.TvDB_Episode.GetNumberOfEpisodesForSeason(ep.SeriesID, ep.SeasonNumber - 1);
        return (ep.SeasonNumber - 1, epsInSeason);
    }
}
