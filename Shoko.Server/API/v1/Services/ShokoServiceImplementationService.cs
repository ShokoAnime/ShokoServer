using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Shoko.Server.API.v1.Models;
using Shoko.Abstractions.Enums;
using Shoko.Abstractions.Extensions;
using Shoko.Server.Extensions;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Providers.AniDB.Release;
using System.IO;
using Shoko.Abstractions.Video.Media;
using Microsoft.Extensions.Logging;

#pragma warning disable CS0618
#nullable enable
namespace Shoko.Server.API.v1.Services;

public class ShokoServiceImplementationService(
    ILogger<ShokoServiceImplementationService> _logger,
    AniDB_Anime_TitleRepository _titles,
    AniDB_CharacterRepository _characters,
    AniDB_TagRepository _aniDBTags,
    AnimeEpisode_UserRepository epUsers,
    AnimeSeries_UserRepository _seriesUsers,
    AnimeSeries_UserRepository seriesUsers,
    StoredReleaseInfoRepository _storedReleaseInfo,
    VideoLocal_UserRepository _vlUsers,
    VideoLocalRepository _videoLocals
)
{
    [return: NotNullIfNotNull(nameof(anime))]
    public CL_AniDB_AnimeDetailed? GetV1DetailedContract(AniDB_Anime? anime, int userid)
    {
        if (anime == null) return null;
        var cl = new CL_AniDB_AnimeDetailed
        {
            AniDBAnime = GetV1Contract(anime),
            AnimeTitles = [],
            Tags = [],
            CustomTags = []
        };

        var animeTitles = _titles.GetByAnimeID(anime.AnimeID);
        if (animeTitles != null)
        {
            foreach (var title in animeTitles)
            {
                cl.AnimeTitles.Add(new()
                {
                    AnimeID = title.AnimeID,
                    Language = title.LanguageCode,
                    Title = title.Title,
                    TitleType = title.TitleType.ToString().ToLower()
                });
            }
        }

        cl.Stat_AllSeasons.UnionWith(anime.YearlySeasons.Select(tuple => $"{tuple.Season} {tuple.Year}"));

        var dictAnimeTags = anime.AnimeTags.ToDictionary(xref => xref.TagID);
        foreach (var tagID in dictAnimeTags.Keys)
        {
            var tag = _aniDBTags.GetByTagID(tagID)!;
            var xref = dictAnimeTags[tag.TagID];
            cl.Tags.Add(new()
            {
                TagID = tag.TagID,
                GlobalSpoiler = tag.GlobalSpoiler ? 1 : 0,
                LocalSpoiler = xref.LocalSpoiler ? 1 : 0,
                Weight = xref.Weight,
                TagName = tag.TagName,
                TagDescription = tag.TagDescription,
            });
        }


        // Get all the custom tags
        foreach (var custag in anime.CustomTags)
        {
            cl.CustomTags.Add(new()
            {
                CustomTagID = custag.CustomTagID,
                TagDescription = custag.TagDescription,
                TagName = custag.TagName,
            });
        }

        if (
            RepoFactory.AnimeSeries.GetByAnimeID(anime.AnimeID) is { } series &&
            RepoFactory.AnimeSeries_User.GetByUserAndSeriesID(userid, series.AnimeSeriesID) is { HasUserRating: true } userData
        )
            cl.UserVote = new()
            {
                AniDB_VoteID = anime.AnimeID,
                EntityID = anime.AnimeID,
                VoteType = (int)userData.UserRatingVoteType.Value,
                VoteValue = userData.AbsoluteUserRating.Value,
            };

        cl.Stat_AudioLanguages = _storedReleaseInfo.GetByAnidbAnimeID(anime.AnimeID)
            .SelectMany(a => a.AudioLanguages?.Select(b => b.GetString()) ?? [])
            .ToHashSet(StringComparer.InvariantCultureIgnoreCase);
        cl.Stat_SubtitleLanguages = _storedReleaseInfo.GetByAnidbAnimeID(anime.AnimeID)
            .SelectMany(a => a.SubtitleLanguages?.Select(b => b.GetString()) ?? [])
            .ToHashSet(StringComparer.InvariantCultureIgnoreCase);
        cl.Stat_AllVideoQuality =
            new HashSet<string>(_videoLocals.GetByAniDBAnimeID(anime.AnimeID).Select(a => a.ReleaseInfo?.LegacySource).WhereNotNullOrDefault(),
                StringComparer.InvariantCultureIgnoreCase);
        cl.Stat_AllVideoQuality_Episodes = new HashSet<string>(
            _videoLocals.GetByAniDBAnimeID(anime.AnimeID).Select(a => (a, a.EpisodeCrossReferences, a.ReleaseInfo))
                .Where(a => a.ReleaseInfo is { } && a.EpisodeCrossReferences.Select(b => b.AniDBEpisode).WhereNotNull().Any(b => b.AnimeID == anime.AnimeID && b.EpisodeType is EpisodeType.Episode))
                .Select(a => a.ReleaseInfo!.LegacySource)
                .WhereNotNullOrDefault()
                .GroupBy(b => b)
                .Select(a => KeyValuePair.Create(a.Key, a.Count()))
                .Where(a => a.Value >= anime.EpisodeCountNormal).Select(a => a.Key), StringComparer.InvariantCultureIgnoreCase);

        return cl;
    }

    [return: NotNullIfNotNull(nameof(anime))]
    public CL_AniDB_Anime? GetV1Contract(AniDB_Anime? anime)
    {
        if (anime == null) return null;
        var characters = GetCharactersContract(anime);
        var movDbFanart = anime.TmdbMovieBackdrops.Concat(anime.TmdbShowBackdrops).Select(i => i.ToClientFanart()).ToList();
        var cl = anime.ToClient();
        cl.FormattedTitle = anime.Title;
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

    public List<CL_AniDB_Character> GetCharactersContract(AniDB_Anime anime)
    {
        return _characters.GetCharactersForAnime(anime.AnimeID).Select(a => a.ToClient()).ToList();
    }

    [return: NotNullIfNotNull(nameof(series))]
    public CL_AnimeSeries_User? GetV1UserContract(AnimeSeries? series, int userid)
    {
        if (series == null) return null;
        var contract = new CL_AnimeSeries_User
        {
            AniDB_ID = series.AniDB_ID,
            AnimeGroupID = series.AnimeGroupID,
            AnimeSeriesID = series.AnimeSeriesID,
            DateTimeUpdated = series.DateTimeUpdated,
            DateTimeCreated = series.DateTimeCreated,
            DefaultAudioLanguage = series.DefaultAudioLanguage,
            DefaultSubtitleLanguage = series.DefaultSubtitleLanguage,
            LatestLocalEpisodeNumber = series.LatestLocalEpisodeNumber,
            LatestEpisodeAirDate = series.LatestEpisodeAirDate,
            AirsOn = series.AirsOn,
            EpisodeAddedDate = series.EpisodeAddedDate,
            MissingEpisodeCount = series.MissingEpisodeCount,
            MissingEpisodeCountGroups = series.MissingEpisodeCountGroups,
            SeriesNameOverride = series.SeriesNameOverride,
            DefaultFolder = null,
            AniDBAnime = GetV1DetailedContract(series.AniDB_Anime, userid)!,
            CrossRefAniDBTvDBV2 = [],
            TvDB_Series = [],
        };
        if (series.TmdbMovieCrossReferences is { Count: > 0 } tmdbMovieXrefs)
        {
            contract.CrossRefAniDBMovieDB = tmdbMovieXrefs[0].ToClient();
            contract.MovieDB_Movie = tmdbMovieXrefs[0].TmdbMovie?.ToClient();
        }

        contract.CrossRefAniDBMAL = series.MalCrossReferences
            .Select(x => new CL_CrossRef_AniDB_MAL()
            {
                CrossRef_AniDB_MALID = x.CrossRef_AniDB_MALID,
                AnimeID = x.AnimeID,
                MALID = x.MALID,
                MALTitle = null,
                CrossRefSource = 1,
                StartEpisodeNumber = 1,
                StartEpisodeType = 1,
            })
            .ToList();
        try
        {

            var rr = _seriesUsers.GetByUserAndSeriesID(userid, series.AnimeSeriesID);
            if (rr is not null)
            {
                contract.UnwatchedEpisodeCount = rr.UnwatchedEpisodeCount;
                contract.WatchedEpisodeCount = rr.WatchedEpisodeCount;
                contract.WatchedDate = rr.WatchedDate;
                contract.PlayedCount = rr.PlayedCount;
                contract.WatchedCount = rr.WatchedCount;
                contract.StoppedCount = rr.StoppedCount;
                contract.AniDBAnime.AniDBAnime.FormattedTitle = series.Title;
                return contract;
            }

            if (contract.AniDBAnime?.AniDBAnime is not null)
            {
                contract.AniDBAnime.AniDBAnime.FormattedTitle = series.Title;
            }
        }
        catch
        {
        }
        return contract;
    }

    public List<CL_VideoDetailed> GetV1VideoDetailedContracts(AnimeEpisode? ep, int userID)
        => ep?.FileCrossReferences
            .Select(xref => xref.VideoLocal)
            .WhereNotNull()
            .Select(v => GetV1DetailedContract(v, userID))
            .WhereNotNull()
            .ToList() ?? [];

    [return: NotNullIfNotNull(nameof(ep))]
    public CL_AnimeEpisode_User? GetV1Contract(AnimeEpisode? ep, int userID)
    {
        if (ep == null) return null;
        var anidbEpisode = ep.AniDB_Episode ?? throw new NullReferenceException($"Unable to find AniDB Episode with id {ep.AniDB_EpisodeID} locally while generating user contract for shoko episode.");
        var seriesUserRecord = seriesUsers.GetByUserAndSeriesID(userID, ep.AnimeSeriesID);
        var episodeUserRecord = epUsers.GetByUserAndEpisodeID(userID, ep.AnimeEpisodeID);
        var contract = new CL_AnimeEpisode_User
        {
            AniDB_EpisodeID = ep.AniDB_EpisodeID,
            AnimeEpisodeID = ep.AnimeEpisodeID,
            AnimeSeriesID = ep.AnimeSeriesID,
            DateTimeCreated = ep.DateTimeCreated,
            DateTimeUpdated = ep.DateTimeUpdated,
            PlayedCount = episodeUserRecord?.PlayedCount ?? 0,
            StoppedCount = episodeUserRecord?.StoppedCount ?? 0,
            WatchedCount = episodeUserRecord?.WatchedCount ?? 0,
            WatchedDate = episodeUserRecord?.WatchedDate,
            AniDB_EnglishName = RepoFactory.AniDB_Episode_Title.GetByEpisodeIDAndLanguage(ep.AniDB_EpisodeID, TitleLanguage.English)
                .FirstOrDefault()?.Title,
            AniDB_RomajiName = RepoFactory.AniDB_Episode_Title.GetByEpisodeIDAndLanguage(ep.AniDB_EpisodeID, TitleLanguage.Romaji)
                .FirstOrDefault()?.Title,
            AniDB_AirDate = anidbEpisode.GetAirDateAsDate(),
            AniDB_LengthSeconds = anidbEpisode.LengthSeconds,
            AniDB_Rating = anidbEpisode.Rating,
            AniDB_Votes = anidbEpisode.Votes,
            EpisodeNumber = anidbEpisode.EpisodeNumber,
            Description = anidbEpisode.Description,
            EpisodeType = (int)anidbEpisode.EpisodeType,
            UnwatchedEpCountSeries = seriesUserRecord?.UnwatchedEpisodeCount ?? 0,
            LocalFileCount = ep.VideoLocals.Count,
        };
        return contract;
    }

    public CL_VideoLocal GetV1Contract(VideoLocal vl, int userID)
    {
        var cl = new CL_VideoLocal
        {
            CRC32 = vl.CRC32,
            DateTimeUpdated = vl.DateTimeUpdated,
            FileName = vl.FileName,
            FileSize = vl.FileSize,
            Hash = vl.Hash,
            HashSource = vl.HashSource,
            IsIgnored = vl.IsIgnored ? 1 : 0,
            IsVariation = vl.IsVariation ? 1 : 0,
            Duration = (long)(vl.MediaInfo?.GeneralStream.Duration ?? 0),
            MD5 = vl.MD5,
            SHA1 = vl.SHA1,
            VideoLocalID = vl.VideoLocalID,
            Places = vl.Places.Select(a => a.ToClient()).ToList()
        };

        var userRecord = _vlUsers.GetByUserAndVideoLocalID(userID, vl.VideoLocalID);
        if (userRecord?.WatchedDate == null)
        {
            cl.IsWatched = 0;
            cl.WatchedDate = null;
        }
        else
        {
            cl.IsWatched = 1;
            cl.WatchedDate = userRecord.WatchedDate;
        }
        cl.ResumePosition = userRecord?.ResumePosition ?? 0;

        try
        {

            if (vl.MediaInfo?.GeneralStream != null) cl.Media = new CL_Media(vl.VideoLocalID, vl.MediaInfo);
        }
        catch (Exception e)
        {
            _logger.LogError("There was an error generating a Desktop client contract: {Ex}", e);
        }

        return cl;
    }

    public CL_VideoDetailed? GetV1DetailedContract(VideoLocal vl, int userID)
    {
        // get the cross ref episode
        var xrefs = vl.EpisodeCrossReferences;
        if (xrefs.Count == 0) return null;

        var userRecord = _vlUsers.GetByUserAndVideoLocalID(userID, vl.VideoLocalID);
        var aniFile = vl.ReleaseInfo is { ReleaseURI: not null } r && r.ReleaseURI.StartsWith(AnidbReleaseProvider.ReleasePrefix) ? r : null; // to prevent multiple db calls
        var relGroup = vl.ReleaseGroup?.ToClient(); // to prevent multiple db calls
        var mediaInfo = vl.MediaInfo as IMediaInfo; // to prevent multiple db calls
        var audioStream = mediaInfo?.AudioStreams is { Count: > 0 } ? mediaInfo.AudioStreams[0] : null;
        var videoStream = mediaInfo?.VideoStream;
        var cl = new CL_VideoDetailed
        {
            Percentage = xrefs[0].Percentage,
            EpisodeOrder = xrefs[0].EpisodeOrder,
            CrossRefSource = 0,
            AnimeEpisodeID = xrefs[0].EpisodeID,
            VideoLocal_FileName = vl.FileName,
            VideoLocal_Hash = vl.Hash,
            VideoLocal_FileSize = vl.FileSize,
            VideoLocalID = vl.VideoLocalID,
            VideoLocal_IsIgnored = vl.IsIgnored ? 1 : 0,
            VideoLocal_IsVariation = vl.IsVariation ? 1 : 0,
            Places = vl.Places.Select(a => a.ToClient()).ToList(),
            VideoLocal_MD5 = vl.MD5,
            VideoLocal_SHA1 = vl.SHA1,
            VideoLocal_CRC32 = vl.CRC32,
            VideoLocal_HashSource = vl.HashSource,
            VideoLocal_IsWatched = userRecord?.WatchedDate == null ? 0 : 1,
            VideoLocal_WatchedDate = userRecord?.WatchedDate,
            VideoLocal_ResumePosition = userRecord?.ResumePosition ?? 0,
            VideoInfo_AudioBitrate = audioStream?.BitRate.ToString(),
            VideoInfo_AudioCodec = audioStream?.Codec.Simplified,
            VideoInfo_Duration = (long)(mediaInfo?.Duration.TotalMilliseconds ?? 0),
            VideoInfo_VideoBitrate = videoStream?.BitRate.ToString() ?? "0",
            VideoInfo_VideoBitDepth = videoStream?.BitDepth.ToString() ?? "0",
            VideoInfo_VideoCodec = videoStream?.Codec.Simplified,
            VideoInfo_VideoFrameRate = videoStream?.FrameRate.ToString(),
            VideoInfo_VideoResolution = videoStream?.Resolution,
            AniDB_File_FileExtension = Path.GetExtension(aniFile?.OriginalFilename) ?? string.Empty,
            AniDB_File_LengthSeconds = (int?)mediaInfo?.Duration.TotalSeconds ?? 0,
            AniDB_AnimeID = xrefs.FirstOrDefault(xref => xref.AnimeID > 0)?.AnimeID,
            AniDB_CRC = vl.CRC32,
            AniDB_MD5 = vl.MD5,
            AniDB_SHA1 = vl.SHA1,
            AniDB_Episode_Rating = 0,
            AniDB_Episode_Votes = 0,
            AniDB_File_AudioCodec = audioStream?.Codec.Simplified ?? string.Empty,
            AniDB_File_VideoCodec = videoStream?.Codec.Simplified ?? string.Empty,
            AniDB_File_VideoResolution = vl.VideoResolution,
            AniDB_Anime_GroupName = aniFile?.GroupName ?? string.Empty,
            AniDB_Anime_GroupNameShort = aniFile?.GroupShortName ?? string.Empty,
            AniDB_File_Description = aniFile?.Comment ?? string.Empty,
            AniDB_File_ReleaseDate = aniFile?.ReleasedAt is { } releasedAt ? (int)(releasedAt.ToDateTime(TimeOnly.MinValue).ToUniversalTime() - DateTime.UnixEpoch).TotalSeconds : 0,
            AniDB_File_Source = aniFile?.LegacySource ?? string.Empty,
            AniDB_FileID = aniFile is not null ? int.Parse(aniFile.ReleaseURI![AnidbReleaseProvider.ReleasePrefix.Length..]!) : 0,
            AniDB_GroupID = relGroup?.GroupID,
            AniDB_File_FileVersion = aniFile?.Version ?? 1,
            AniDB_File_IsCensored = aniFile?.IsCensored ?? false ? 1 : 0,
            AniDB_File_IsChaptered = aniFile?.IsChaptered ?? false ? 1 : 0,
            AniDB_File_IsDeprecated = aniFile?.IsCorrupted ?? false ? 1 : 0,
            AniDB_File_InternalVersion = 4,
            LanguagesAudio = aniFile?.AudioLanguages?.Select(a => a.GetString()).Join(',') ?? string.Empty,
            LanguagesSubtitle = aniFile?.SubtitleLanguages?.Select(a => a.GetString()).Join(',') ?? string.Empty,
            ReleaseGroup = relGroup,
            Media = mediaInfo is null ? null : new CL_Media(vl.VideoLocalID, mediaInfo),
        };

        return cl;
    }
}
