using System;
using Shoko.Server.API.v1.Models;
using Shoko.Server.API.v1.Models.Metro;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Models.Trakt;
using Shoko.Server.Providers.TraktTV.Contracts;

namespace Shoko.Server.Extensions;

public static class ModelProviders
{
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

    public static void Populate(this AnimeGroup group, AnimeSeries series)
    {
        group.Populate(series, DateTime.Now);
    }

    public static void Populate(this AnimeGroup group, AnimeSeries series, DateTime now)
    {
        group.Description = series.PreferredOverview?.Value ?? string.Empty;
        var name = series.Title;
        group.GroupName = name;
        group.MainAniDBAnimeID = series.AniDB_ID;
        group.DateTimeUpdated = now;
        group.DateTimeCreated = now;
    }

    public static void Populate(this AnimeGroup group, AniDB_Anime anime, DateTime now)
    {
        group.Description = anime.Description;
        var name = anime.Title;
        group.GroupName = name;
        group.MainAniDBAnimeID = anime.AnimeID;
        group.DateTimeUpdated = now;
        group.DateTimeCreated = now;
    }

    public static void Populate(this AnimeEpisode episode, AniDB_Episode anidbEpisode)
    {
        episode.AniDB_EpisodeID = anidbEpisode.EpisodeID;
        episode.DateTimeUpdated = DateTime.Now;
        episode.DateTimeCreated = DateTime.Now;
    }
}
