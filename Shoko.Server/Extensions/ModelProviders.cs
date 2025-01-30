using System;
using Shoko.Models.Enums;
using Shoko.Models.Metro;
using Shoko.Server.Models;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.Trakt;
using Shoko.Server.Providers.TraktTV.Contracts;

namespace Shoko.Server.Extensions;

public static class ModelProviders
{
    public static void Populate(this Trakt_Show show, TraktV2ShowExtended tvShow)
    {
        show.Overview = tvShow.Overview;
        show.Title = tvShow.Title;
        show.TraktID = tvShow.IDs.TraktSlug;
        show.TmdbShowID = tvShow.IDs.TmdbID;
        show.URL = tvShow.URL;
        show.Year = tvShow.Year.ToString();
    }

    public static Metro_AniDB_Character ToContractMetro(this AniDB_Character character,
        AniDB_Anime_Character charRel)
    {
        var contract = new Metro_AniDB_Character
        {
            AniDB_CharacterID = character.AniDB_CharacterID,
            CharID = character.CharacterID,
            CharName = character.Name,
            CharKanjiName = character.OriginalName,
            CharDescription = character.Description,
            CharType = charRel.Appearance,
            ImageType = (int)CL_ImageEntityType.AniDB_Character,
            ImageID = character.AniDB_CharacterID
        };
        var creator = charRel.Creators is { Count: > 0 } ? charRel.Creators[0] : null;
        if (creator != null)
        {
            contract.SeiyuuID = creator.AniDB_CreatorID;
            contract.SeiyuuName = creator.Name;
            contract.SeiyuuImageType = (int)CL_ImageEntityType.AniDB_Creator;
            contract.SeiyuuImageID = creator.CreatorID;
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
}
