using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Commons.Extensions;
using Shoko.Models.Client;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Plugin.Abstractions.Extensions;
using Shoko.Server.Extensions;
using Shoko.Server.Models;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Cached.AniDB;

namespace Shoko.Server.Services;

public class AniDB_AnimeService
{
    private readonly AniDB_Anime_TitleRepository _titles;
    private readonly AniDB_TagRepository _aniDBTags;
    private readonly DatabaseReleaseInfoRepository _databaseReleaseInfo;
    private readonly VideoLocalRepository _videoLocals;
    private readonly AniDB_CharacterRepository _characters;

    public AniDB_AnimeService(AniDB_Anime_TitleRepository titles, AniDB_TagRepository aniDBTags, DatabaseReleaseInfoRepository databaseReleaseInfo, VideoLocalRepository videoLocals, AniDB_CharacterRepository characters)
    {
        _titles = titles;
        _aniDBTags = aniDBTags;
        _databaseReleaseInfo = databaseReleaseInfo;
        _videoLocals = videoLocals;
        _characters = characters;
    }

    public CL_AniDB_AnimeDetailed GetV1DetailedContract(SVR_AniDB_Anime anime)
    {
        if (anime == null) return null;
        var cl = new CL_AniDB_AnimeDetailed
        {
            AniDBAnime = GetV1Contract(anime),
            AnimeTitles = new List<CL_AnimeTitle>(),
            Tags = new List<CL_AnimeTag>(),
            CustomTags = new List<CustomTag>()
        };

        var animeTitles = _titles.GetByAnimeID(anime.AnimeID);
        if (animeTitles != null)
        {
            foreach (var title in animeTitles)
            {
                var ctitle = new CL_AnimeTitle
                {
                    AnimeID = title.AnimeID,
                    Language = title.LanguageCode,
                    Title = title.Title,
                    TitleType = title.TitleType.ToString().ToLower()
                };
                cl.AnimeTitles.Add(ctitle);
            }
        }

        cl.Stat_AllSeasons.UnionWith(anime.Seasons.Select(tuple => $"{tuple.Season} {tuple.Year}"));

        var dictAnimeTags = anime.AnimeTags.ToDictionary(xref => xref.TagID);
        foreach (var tagID in dictAnimeTags.Keys)
        {
            var tag = _aniDBTags.GetByTagID(tagID);
            var ctag = new CL_AnimeTag
            {
                TagID = tag.TagID,
                GlobalSpoiler = tag.GlobalSpoiler ? 1 : 0,
                LocalSpoiler = 0,
                Weight = 0,
                TagName = tag.TagName,
                TagDescription = tag.TagDescription,
            };

            var xref = dictAnimeTags[tag.TagID];
            ctag.LocalSpoiler = xref.LocalSpoiler ? 1 : 0;
            ctag.Weight = xref.Weight;

            cl.Tags.Add(ctag);
        }


        // Get all the custom tags
        foreach (var custag in anime.CustomTags)
        {
            cl.CustomTags.Add(custag);
        }

        cl.UserVote = anime.UserVote;

        cl.Stat_AudioLanguages = _databaseReleaseInfo.GetByAnidbAnimeID(anime.AnimeID)
            .SelectMany(a => a.AudioLanguages?.Select(b => b.GetString()) ?? [])
            .ToHashSet(StringComparer.InvariantCultureIgnoreCase);
        cl.Stat_SubtitleLanguages = _databaseReleaseInfo.GetByAnidbAnimeID(anime.AnimeID)
            .SelectMany(a => a.SubtitleLanguages?.Select(b => b.GetString()) ?? [])
            .ToHashSet(StringComparer.InvariantCultureIgnoreCase);
        cl.Stat_AllVideoQuality =
            new HashSet<string>(_videoLocals.GetByAniDBAnimeID(anime.AnimeID).Select(a => a.ReleaseInfo?.LegacySource).WhereNotDefault(),
                StringComparer.InvariantCultureIgnoreCase);
        cl.Stat_AllVideoQuality_Episodes = new HashSet<string>(
            _videoLocals.GetByAniDBAnimeID(anime.AnimeID).Select(a => (a, a.EpisodeCrossReferences, a.ReleaseInfo))
                .Where(a => a.ReleaseInfo is { } && a.EpisodeCrossReferences.Select(b => b.AniDBEpisode).WhereNotNull().Any(b => b.AnimeID == anime.AnimeID && b.EpisodeType == (int)EpisodeType.Episode))
                .Select(a => a.ReleaseInfo!.LegacySource)
                .WhereNotDefault()
                .GroupBy(b => b)
                .Select(a => KeyValuePair.Create(a.Key, a.Count()))
                .Where(a => a.Value >= anime.EpisodeCountNormal).Select(a => a.Key), StringComparer.InvariantCultureIgnoreCase);

        return cl;
    }

    public CL_AniDB_Anime GetV1Contract(SVR_AniDB_Anime anime)
    {
        if (anime == null) return null;
        var characters = GetCharactersContract(anime);
        var movDbFanart = anime.TmdbMovieBackdrops.Concat(anime.TmdbShowBackdrops).Select(i => i.ToClientFanart()).ToList();
        var cl = anime.ToClient();
        cl.FormattedTitle = anime.PreferredTitle;
        cl.Characters = characters;
        cl.Banners = null;
        cl.Fanarts = [];
        if (movDbFanart != null && movDbFanart.Count != 0)
        {
            cl.Fanarts.AddRange(movDbFanart.Select(a => new CL_AniDB_Anime_DefaultImage
            {
                ImageType = (int)CL_ImageEntityType.MovieDB_FanArt,
                MovieFanart = a,
                AniDB_Anime_DefaultImageID = a.MovieDB_FanartID,
            }));
        }
        if (cl.Fanarts?.Count == 0)
            cl.Fanarts = null;
        cl.DefaultImageFanart = anime.PreferredBackdrop?.ToClient();
        cl.DefaultImagePoster = anime.PreferredPoster?.ToClient();
        cl.DefaultImageWideBanner = null;
        return cl;
    }

    public List<CL_AniDB_Character> GetCharactersContract(SVR_AniDB_Anime anime)
    {
        return _characters.GetCharactersForAnime(anime.AnimeID).Select(a => a.ToClient()).ToList();
    }
}
