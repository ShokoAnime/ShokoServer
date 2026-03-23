using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.User.Enums;
using Shoko.Abstractions.Video.Media;
using Shoko.Server.API.v1.Models;
using Shoko.Server.Extensions;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Providers.AniDB.Release;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Cached.AniDB;

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
    AnimeGroup_UserRepository _groupUsers,
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

    [return: NotNullIfNotNull(nameof(group))]
    public CL_AnimeGroup_User? GetV1Contract(AnimeGroup? group, int userid)
    {
        if (group == null) return null;
        var groupSeries = group.AllSeries;
        var mainSeries = group.MainSeries ?? groupSeries.FirstOrDefault();
        var userDict = groupSeries
            .Select(a => _seriesUsers.GetByUserAndSeriesID(userid, a.AnimeSeriesID))
            .WhereNotNull()
            .ToDictionary(a => a.AnimeSeriesID);
        var votesByAnime = userDict.Values
            .Where(x => x.HasUserRating)
            .ToDictionary(a => a.AnimeSeriesID);
        var isFavorite = mainSeries is not null && userDict.TryGetValue(mainSeries.AnimeSeriesID, out var mainSeriesUser) && mainSeriesUser.IsFavorite;
        var contract = GetContract(group);
        var allVoteTotal = 0D;
        var permVoteTotal = 0D;
        var tempVoteTotal = 0D;
        var allVoteCount = 0;
        var permVoteCount = 0;
        var tempVoteCount = 0;
        foreach (var series in groupSeries)
        {
            if (votesByAnime.TryGetValue(series.AnimeSeriesID, out var seriesUserData))
            {
                allVoteCount++;
                allVoteTotal += seriesUserData.UserRating!.Value;

                switch (seriesUserData.UserRatingVoteType!.Value)
                {
                    case SeriesVoteType.Permanent:
                        permVoteCount++;
                        permVoteTotal += seriesUserData.UserRating!.Value;
                        break;
                    case SeriesVoteType.Temporary:
                        tempVoteCount++;
                        tempVoteTotal += seriesUserData.UserRating!.Value;
                        break;
                }
            }
        }
        var groupUserData = _groupUsers.GetByUserAndGroupID(userid, group.AnimeGroupID);
        if (groupUserData is not null)
        {
            contract.UnwatchedEpisodeCount = groupUserData.UnwatchedEpisodeCount;
            contract.WatchedEpisodeCount = groupUserData.WatchedEpisodeCount;
            contract.WatchedDate = groupUserData.WatchedDate;
            contract.PlayedCount = groupUserData.PlayedCount;
            contract.WatchedCount = groupUserData.WatchedCount;
            contract.StoppedCount = groupUserData.StoppedCount;
        }
        contract.IsFave = isFavorite ? 1 : 0;
        contract.Stat_UserVoteOverall = allVoteCount == 0 ? null : (decimal)Math.Round(allVoteTotal / allVoteCount / 100D, 2);
        contract.Stat_UserVotePermanent = permVoteCount == 0 ? null : (decimal)Math.Round(permVoteTotal / permVoteCount / 100D, 2);
        contract.Stat_UserVoteTemporary = tempVoteCount == 0 ? null : (decimal)Math.Round(tempVoteTotal / tempVoteCount / 100D, 2);

        return contract;
    }

    [return: NotNullIfNotNull(nameof(animeGroup))]
    public CL_AnimeGroup_User? GetContract(AnimeGroup? animeGroup)
    {
        if (animeGroup == null) return null;
        var now = DateTime.Now;

        var contract = new CL_AnimeGroup_User
        {
            AnimeGroupID = animeGroup.AnimeGroupID,
            AnimeGroupParentID = animeGroup.AnimeGroupParentID,
            DefaultAnimeSeriesID = animeGroup.DefaultAnimeSeriesID,
            GroupName = animeGroup.GroupName,
            Description = animeGroup.Description,
            LatestEpisodeAirDate = animeGroup.LatestEpisodeAirDate,
            SortName = animeGroup.GroupName.ToSortName(),
            EpisodeAddedDate = animeGroup.EpisodeAddedDate,
            OverrideDescription = animeGroup.OverrideDescription,
            DateTimeUpdated = animeGroup.DateTimeUpdated,
            IsFave = 0,
            UnwatchedEpisodeCount = 0,
            WatchedEpisodeCount = 0,
            WatchedDate = null,
            PlayedCount = 0,
            WatchedCount = 0,
            StoppedCount = 0,
            MissingEpisodeCount = animeGroup.MissingEpisodeCount,
            MissingEpisodeCountGroups = animeGroup.MissingEpisodeCountGroups
        };

        var allSeriesForGroup = animeGroup.AllSeries;
        var allIDs = allSeriesForGroup.Select(a => a.AniDB_ID).ToArray();

        DateTime? airDateMin = null;
        DateTime? airDateMax = null;
        DateTime? groupEndDate = new DateTime(1980, 1, 1);
        DateTime? seriesCreatedDate = null;
        var isComplete = false;
        var hasFinishedAiring = false;
        var isCurrentlyAiring = false;
        var videoQualityEpisodes = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
        var allVidQualByGroup = allSeriesForGroup
            .SelectMany(a => _storedReleaseInfo.GetByAnidbAnimeID(a.AniDB_ID))
            .Select(a => a.LegacySource)
            .ToHashSet(StringComparer.InvariantCultureIgnoreCase);
        var tmdbShowXrefByAnime = allIDs
            .Select(RepoFactory.CrossRef_AniDB_TMDB_Show.GetByAnidbAnimeID)
            .Where(a => a is { Count: > 0 })
            .ToDictionary(a => a[0].AnidbAnimeID);
        var tmdbMovieXrefByAnime = allIDs
            .Select(RepoFactory.CrossRef_AniDB_TMDB_Movie.GetByAnidbAnimeID)
            .Where(a => a is { Count: > 0 })
            .ToDictionary(a => a[0].AnidbAnimeID);
        var malXRefByAnime = allIDs.SelectMany(RepoFactory.CrossRef_AniDB_MAL.GetByAnimeID).ToLookup(a => a.AnimeID);
        // Even though the contract value says 'has link', it's easier to think about whether it's missing
        var missingMALLink = false;
        var missingTMDBLink = false;
        var seriesCount = 0;
        var epCount = 0;

        var allYears = new HashSet<int>();
        var allSeasons = new SortedSet<string>(new CL_SeasonComparator());

        foreach (var series in allSeriesForGroup)
        {
            seriesCount++;

            var vidsTemp = RepoFactory.VideoLocal.GetByAniDBAnimeID(series.AniDB_ID);
            var crossRefs = RepoFactory.CrossRef_File_Episode.GetByAnimeID(series.AniDB_ID);
            var crossRefsLookup = crossRefs.ToLookup(cr => cr.EpisodeID);
            var dictVids = new Dictionary<string, VideoLocal>();

            foreach (var vid in vidsTemp)
            // Hashes may be repeated from multiple locations, but we don't care
            {
                dictVids[vid.Hash] = vid;
            }

            // All Video Quality Episodes
            // Try to determine if this anime has all the episodes available at a certain video quality
            // e.g.  the series has all episodes in blu-ray
            // Also look at languages
            var vidQualEpCounts = new Dictionary<string, int>();
            // video quality, count of episodes
            var anime = series.AniDB_Anime!;

            foreach (var ep in series.AllAnimeEpisodes)
            {
                if (ep.AniDB_Episode is null || ep.EpisodeType is not EpisodeType.Episode)
                {
                    continue;
                }

                var epVids = new List<VideoLocal>();

                foreach (var xref in crossRefsLookup[ep.AniDB_EpisodeID])
                {
                    if (xref.EpisodeID != ep.AniDB_EpisodeID)
                    {
                        continue;
                    }


                    if (dictVids.TryGetValue(xref.Hash, out var video))
                    {
                        epVids.Add(video);
                    }
                }

                var qualityAddedSoFar = new HashSet<string>();

                // Handle mutliple files of the same quality for one episode
                foreach (var vid in epVids)
                {
                    var release = vid.ReleaseInfo;

                    if (release == null)
                    {
                        continue;
                    }

                    if (!qualityAddedSoFar.Contains(release.LegacySource))
                    {
                        vidQualEpCounts.TryGetValue(release.LegacySource, out var srcCount);
                        vidQualEpCounts[release.LegacySource] =
                            srcCount +
                            1; // If the file source wasn't originally in the dictionary, then it will be set to 1

                        qualityAddedSoFar.Add(release.LegacySource);
                    }
                }
            }

            epCount += anime.EpisodeCountNormal;

            // Add all video qualities that span all of the normal episodes
            videoQualityEpisodes.UnionWith(
                vidQualEpCounts
                    .Where(vqec => anime.EpisodeCountNormal == vqec.Value)
                    .Select(vqec => vqec.Key));

            // Calculate Air Date
            var seriesAirDate = series.AirDate;

            if (seriesAirDate.HasValue)
            {
                if (airDateMin == null || seriesAirDate.Value < airDateMin.Value)
                {
                    airDateMin = seriesAirDate.Value.ToDateTime();
                }

                if (airDateMax == null || seriesAirDate.Value > airDateMax.Value)
                {
                    airDateMax = seriesAirDate.Value.ToDateTime();
                }
            }

            // Calculate end date
            // If the end date is NULL it actually means it is ongoing, so this is the max possible value
            var seriesEndDate = series.EndDate;

            if (seriesEndDate is null || groupEndDate is null)
            {
                groupEndDate = null;
            }
            else if (seriesEndDate.Value > groupEndDate.Value)
            {
                groupEndDate = seriesEndDate?.ToDateTime();
            }

            // Note - only one series has to be finished airing to qualify
            if (series.EndDate is not null && series.EndDate.Value < now)
            {
                hasFinishedAiring = true;
            }

            // Note - only one series has to be finished airing to qualify
            if (series.EndDate is null || series.EndDate.Value > now)
            {
                isCurrentlyAiring = true;
            }

            // We evaluate IsComplete as true if
            // 1. series has finished airing
            // 2. user has all episodes locally
            // Note - only one series has to be complete for the group to be considered complete
            if (series.EndDate is not null && series.EndDate.Value < now
                                       && series.MissingEpisodeCount == 0 &&
                                       series.MissingEpisodeCountGroups == 0)
            {
                isComplete = true;
            }

            // Calculate Series Created Date
            var createdDate = series.DateTimeCreated;

            if (seriesCreatedDate == null || createdDate < seriesCreatedDate.Value)
            {
                seriesCreatedDate = createdDate;
            }

            // For the group, if any of the series don't have a tmdb link
            // we will consider the group as not having a tmdb link
            var foundTMDBShowLink = tmdbShowXrefByAnime.TryGetValue(anime.AnimeID, out var _);
            var foundTMDBMovieLink = tmdbMovieXrefByAnime.TryGetValue(anime.AnimeID, out var _);
            var isMovie = anime.AnimeType is AnimeType.Movie;

            if (!foundTMDBShowLink && !foundTMDBMovieLink)
            {
                if (!series.IsTMDBAutoMatchingDisabled)
                {
                    missingTMDBLink = true;
                }
            }

            if (!malXRefByAnime[anime.AnimeID].Any())
            {
                missingMALLink = true;
            }

            var endYear = anime.EndYear;
            if (endYear == 0)
            {
                endYear = DateTime.Today.Year;
            }

            var startYear = anime.BeginYear;
            if (endYear < startYear)
            {
                endYear = startYear;
            }

            if (startYear != 0)
            {
                List<int> years;
                if (startYear == endYear)
                {
                    years = new List<int>
                    {
                        startYear
                    };
                }
                else
                {
                    years = Enumerable.Range(anime.BeginYear, endYear - anime.BeginYear + 1)
                        .Where(anime.IsInYear).ToList();
                }

                allYears.UnionWith(years);
                allSeasons.UnionWith(anime.YearlySeasons.Select(tuple => $"{tuple.Season} {tuple.Year}"));
            }
        }

        contract.Stat_AllYears = allYears;
        contract.Stat_AllSeasons = allSeasons;
        contract.Stat_AllTags = animeGroup.Tags.Select(a => a.TagName.Trim()).ToHashSet(StringComparer.InvariantCultureIgnoreCase);
        contract.Stat_AllCustomTags = animeGroup.CustomTags.Select(a => a.TagName).ToHashSet(StringComparer.InvariantCultureIgnoreCase);
        contract.Stat_AllTitles = animeGroup.Titles.Select(a => a.Title).ToHashSet(StringComparer.InvariantCultureIgnoreCase);
        contract.Stat_AnimeTypes = allSeriesForGroup.Select(a => a.AniDB_Anime!.AnimeType.ToString().Replace('_', ' ')).WhereNotNull().ToHashSet(StringComparer.InvariantCultureIgnoreCase);
        contract.Stat_AllVideoQuality = allVidQualByGroup;
        contract.Stat_IsComplete = isComplete;
        contract.Stat_HasFinishedAiring = hasFinishedAiring;
        contract.Stat_IsCurrentlyAiring = isCurrentlyAiring;
        contract.Stat_HasTvDBLink = false; // Deprecated
        contract.Stat_HasTraktLink = false; // Has a link if it isn't missing
        contract.Stat_HasMALLink = !missingMALLink; // Has a link if it isn't missing
        contract.Stat_HasMovieDBLink = !missingTMDBLink; // Has a link if it isn't missing
        contract.Stat_HasMovieDBOrTvDBLink = !missingTMDBLink; // Has a link if it isn't missing
        contract.Stat_SeriesCount = seriesCount;
        contract.Stat_EpisodeCount = epCount;
        contract.Stat_AllVideoQuality_Episodes = videoQualityEpisodes;
        contract.Stat_AirDate_Min = airDateMin;
        contract.Stat_AirDate_Max = airDateMax;
        contract.Stat_EndDate = groupEndDate;
        contract.Stat_SeriesCreatedDate = seriesCreatedDate;
        contract.Stat_AniDBRating = animeGroup.AniDBRating;
        contract.Stat_AudioLanguages = animeGroup.AllSeries
            .Select(a => a.AniDB_Anime)
            .WhereNotNull()
            .SelectMany(a => _storedReleaseInfo.GetByAnidbAnimeID(a.AnimeID))
            .SelectMany(a => a.AudioLanguages?.Select(b => b.GetString()) ?? [])
            .ToHashSet(StringComparer.InvariantCultureIgnoreCase);
        contract.Stat_SubtitleLanguages = animeGroup.AllSeries
            .Select(a => a.AniDB_Anime)
            .WhereNotNull()
            .SelectMany(a => _storedReleaseInfo.GetByAnidbAnimeID(a.AnimeID))
            .SelectMany(a => a.SubtitleLanguages?.Select(b => b.GetString()) ?? [])
            .ToHashSet(StringComparer.InvariantCultureIgnoreCase);
        contract.LatestEpisodeAirDate = animeGroup.LatestEpisodeAirDate;

        return contract;
    }
}
