﻿using System.IO;
using Shoko.Commons.Extensions;
using Shoko.Commons.Properties;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server.ImageDownload;

namespace Shoko.Server.Extensions;

public static class ImageResolvers
{
    public static string GetFullImagePath(this MovieDB_Fanart fanart)
    {
        if (string.IsNullOrEmpty(fanart.URL))
        {
            return string.Empty;
        }

        //strip out the base URL
        var pos = fanart.URL.IndexOf('/', 0);
        var fname = fanart.URL.Substring(pos + 1, fanart.URL.Length - pos - 1);
        fname = fname.Replace("/", $"{Path.DirectorySeparatorChar}");
        return Path.Combine(ImageUtils.GetMovieDBImagePath(), fname);
    }

    public static string GetFullImagePath(this MovieDB_Poster movie)
    {
        if (string.IsNullOrEmpty(movie.URL))
        {
            return string.Empty;
        }

        //strip out the base URL
        var pos = movie.URL.IndexOf('/', 0);
        var fname = movie.URL.Substring(pos + 1, movie.URL.Length - pos - 1);
        fname = fname.Replace("/", $"{Path.DirectorySeparatorChar}");
        return Path.Combine(ImageUtils.GetMovieDBImagePath(), fname);
    }

    public static string GetFullImagePath(this TvDB_Episode episode)
    {
        if (string.IsNullOrEmpty(episode.Filename))
        {
            return string.Empty;
        }

        var fname = episode.Filename;
        fname = episode.Filename.Replace("/", $"{Path.DirectorySeparatorChar}");
        return Path.Combine(ImageUtils.GetTvDBImagePath(), fname);
    }

    public static string GetFullImagePath(this TvDB_ImageFanart fanart)
    {
        if (string.IsNullOrEmpty(fanart.BannerPath))
        {
            return string.Empty;
        }

        var fname = fanart.BannerPath;
        fname = fanart.BannerPath.Replace("/", $"{Path.DirectorySeparatorChar}");
        return Path.Combine(ImageUtils.GetTvDBImagePath(), fname);
    }

    public static string GetFullImagePath(this TvDB_ImagePoster poster)
    {
        if (string.IsNullOrEmpty(poster.BannerPath))
        {
            return string.Empty;
        }

        var fname = poster.BannerPath;
        fname = poster.BannerPath.Replace("/", $"{Path.DirectorySeparatorChar}");
        return Path.Combine(ImageUtils.GetTvDBImagePath(), fname);
    }

    public static string GetFullImagePath(this TvDB_ImageWideBanner banner)
    {
        if (string.IsNullOrEmpty(banner.BannerPath))
        {
            return string.Empty;
        }

        var fname = banner.BannerPath;
        fname = banner.BannerPath.Replace("/", $"{Path.DirectorySeparatorChar}");
        return Path.Combine(ImageUtils.GetTvDBImagePath(), fname);
    }

    public static void Valid(this TvDB_ImageFanart fanart)
    {
        if (!File.Exists(fanart.GetFullImagePath()))
        {
            //clean leftovers
            if (File.Exists(fanart.GetFullImagePath()))
            {
                File.Delete(fanart.GetFullImagePath());
            }
        }
    }

    public static string GetPosterPath(this AniDB_Character character)
    {
        if (string.IsNullOrEmpty(character.PicName))
        {
            return string.Empty;
        }

        return Path.Combine(ImageUtils.GetAniDBCharacterImagePath(character.CharID), character.PicName);
    }

    public static string GetPosterPath(this AniDB_Seiyuu seiyuu)
    {
        if (string.IsNullOrEmpty(seiyuu.PicName))
        {
            return string.Empty;
        }

        return Path.Combine(ImageUtils.GetAniDBCreatorImagePath(seiyuu.SeiyuuID), seiyuu.PicName);
    }

    //The resources need to be moved
    public static string GetAnimeTypeDescription(this AniDB_Anime anidbanime)
    {
        switch (anidbanime.GetAnimeTypeEnum())
        {
            case AnimeType.Movie:
                return Resources.AnimeType_Movie;
            case AnimeType.Other:
                return Resources.AnimeType_Other;
            case AnimeType.OVA:
                return Resources.AnimeType_OVA;
            case AnimeType.TVSeries:
                return Resources.AnimeType_TVSeries;
            case AnimeType.TVSpecial:
                return Resources.AnimeType_TVSpecial;
            case AnimeType.Web:
                return Resources.AnimeType_Web;
            default:
                return Resources.AnimeType_Other;
        }
    }
}
