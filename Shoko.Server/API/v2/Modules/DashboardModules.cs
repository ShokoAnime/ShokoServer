﻿using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shoko.Commons.Extensions;
using Shoko.Server.Extensions;
using Shoko.Server.Models;
using Shoko.Server.Repositories;
using Shoko.Server.Server;
using Shoko.Server.Settings;

namespace Shoko.Server.API.v2.Modules;

//As responds for this API we throw object that will be converted to json/xml
[Authorize]
[ApiController]
[Route("/api/modules")]
[ApiVersion("2.0")]
public class DashboardModules : BaseController
{
    // class will be found automagicly thanks to inherits also class need to be public (or it will 404)  

    /// <summary>
    /// Return Dictionary with nesesery items for Dashboard inside Webui
    /// </summary>
    /// <returns>Dictionary<string, object></returns>
    [HttpGet]
    public object GetStats()
    {
        var user = HttpContext.User.Identity as SVR_JMMUser;

        int series_count;
        int file_count;
        string size;

        var watched_files = 0;
        var watched_series = 0;
        decimal hours = 0;

        List<string> tags;

        if (user != null)
        {
            var series = RepoFactory.AnimeSeries.GetAll().Where(a =>
                !a.GetAnime()?.GetAllTags().FindInEnumerable(user.GetHideCategories()) ?? false).ToList();
            series_count = series.Count;

            var files = series.SelectMany(a => a.GetAnimeEpisodes()).SelectMany(a => a.GetVideoLocals())
                .DistinctBy(a => a.VideoLocalID).ToList();
            file_count = files.Count;
            size = SizeSuffix(files.Sum(a => a.FileSize));

            var watched = RepoFactory.VideoLocalUser.GetByUserID(user.JMMUserID)
                .Where(a => a.WatchedDate != null).ToList();

            watched_files = watched.Count;

            watched_series = RepoFactory.AnimeSeries.GetAll().Count(a =>
            {
                var contract = a.GetUserContract(user.JMMUserID);
                if (contract?.MissingEpisodeCount > 0)
                {
                    return false;
                }

                return contract?.UnwatchedEpisodeCount == 0;
            });

            hours = Math.Round((decimal)watched.Select(a => RepoFactory.VideoLocal.GetByID(a.VideoLocalID))
                    .Where(a => a != null)
                    .Sum(a => a.Media?.GeneralStream?.Duration ?? 0) / 3600, 1,
                MidpointRounding.AwayFromZero); // 60s * 60m = ?h

            tags = RepoFactory.AniDB_Anime_Tag.GetAllForLocalSeries().GroupBy(a => a.TagID)
                .ToDictionary(a => a.Key, a => a.Count()).OrderByDescending(a => a.Value)
                .Select(a => RepoFactory.AniDB_Tag.GetByTagID(a.Key)?.TagName)
                .Where(a => a != null && !user.GetHideCategories().Contains(a)).ToList();
            var tagfilter = TagFilter.Filter.AnidbInternal | TagFilter.Filter.Misc | TagFilter.Filter.Source;
            tags = TagFilter.String.ProcessTags(tagfilter, tags).Take(10).ToList();
        }
        else
        {
            var series = RepoFactory.AnimeSeries.GetAll();
            series_count = series.Count;

            var files = series.SelectMany(a => a.GetAnimeEpisodes()).SelectMany(a => a.GetVideoLocals())
                .DistinctBy(a => a.VideoLocalID).ToList();
            file_count = files.Count;
            size = SizeSuffix(files.Sum(a => a.FileSize));

            tags = RepoFactory.AniDB_Anime_Tag.GetAllForLocalSeries().GroupBy(a => a.TagID)
                .ToDictionary(a => a.Key, a => a.Count()).OrderByDescending(a => a.Value)
                .Select(a => RepoFactory.AniDB_Tag.GetByTagID(a.Key)?.TagName)
                .Where(a => a != null).ToList();
            var tagfilter = TagFilter.Filter.AnidbInternal | TagFilter.Filter.Misc | TagFilter.Filter.Source;
            tags = TagFilter.String.ProcessTags(tagfilter, tags).Take(10).ToList();
        }

        return new Dictionary<string, object>
        {
            {
                "queue", RepoFactory.CommandRequest.GetAll().GroupBy(a => a.CommandType)
                    .ToDictionary(a => (CommandRequestType)a.Key, a => a.Count())
            },
            { "file_count", file_count },
            { "series_count", series_count },
            { "collection_size", size },
            { "watched_files", watched_files },
            { "watched_series", watched_series },
            { "hours_watched", hours },
            { "tags", tags }
        };
    }

    private static readonly string[] SizeSuffixes = { "bytes", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB" };

    private static string SizeSuffix(long value, int decimalPlaces = 1)
    {
        if (decimalPlaces < 0) { throw new ArgumentOutOfRangeException(nameof(decimalPlaces)); }

        if (value < 0)
        {
            return "-" + SizeSuffix(-value);
        }

        if (value == 0)
        {
            return string.Format("{0:n" + decimalPlaces + "} bytes", 0);
        }

        // mag is 0 for bytes, 1 for KB, 2, for MB, etc.
        var mag = (int)Math.Log(value, 1024);

        // 1L << (mag * 10) == 2 ^ (10 * mag) 
        // [i.e. the number of bytes in the unit corresponding to mag]
        var adjustedSize = (decimal)value / (1L << (mag * 10));

        // make adjustment when the value is large enough that
        // it would round up to 1000 or more
        if (Math.Round(adjustedSize, decimalPlaces) >= 1000)
        {
            mag++;
            adjustedSize /= 1024;
        }

        return string.Format("{0:n" + decimalPlaces + "} {1}", adjustedSize, SizeSuffixes[mag]);
    }

    public DashboardModules(ISettingsProvider settingsProvider) : base(settingsProvider)
    {
    }
}
