using System;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.Shoko;

namespace Shoko.Server.Extensions;

public static class ModelProviders
{
    public static void Populate(this AnimeGroup group, AnimeSeries series, DateTime now)
    {
        group.MainAniDBAnimeID = series.AniDB_ID;
        group.DateTimeUpdated = now;
        group.DateTimeCreated = now;
    }

    public static void Populate(this AnimeGroup group, AniDB_Anime anime, DateTime now)
    {
        group.MainAniDBAnimeID = anime.AnimeID;
        group.DateTimeUpdated = now;
        group.DateTimeCreated = now;
    }
}
