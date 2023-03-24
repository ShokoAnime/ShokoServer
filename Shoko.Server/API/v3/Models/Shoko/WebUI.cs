using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Commons.Extensions;
using Shoko.Models.Enums;
using Shoko.Models.MediaInfo;
using Shoko.Server.API.Converters;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.Models;
using Shoko.Server.Repositories;

using AnimeType = Shoko.Models.Enums.AnimeType;

namespace Shoko.Server.API.v3.Models.Shoko;

public class WebUI
{
    public class WebUIGroupExtra
    {
        public WebUIGroupExtra(SVR_AnimeGroup group, SVR_AnimeSeries series, SVR_AniDB_Anime anime,
            TagFilter.Filter filter = TagFilter.Filter.None, bool orderByName = false, int tagLimit = 30)
        {
            ID = group.AnimeGroupID;
            Type = Series.GetAniDBSeriesType(anime.AnimeType);
            Rating = new Rating { Source = "AniDB", Value = anime.Rating, MaxValue = 1000, Votes = anime.VoteCount };
            if (anime.AirDate != null)
            {
                var airdate = anime.AirDate.Value;
                if (airdate != DateTime.MinValue)
                {
                    AirDate = airdate;
                }
            }

            if (anime.EndDate != null)
            {
                var enddate = anime.EndDate.Value;
                if (enddate != DateTime.MinValue)
                {
                    EndDate = enddate;
                }
            }

            Tags = Series.GetTags(anime, filter, excludeDescriptions: true, orderByName)
                .Take(tagLimit)
                .ToList();
        }

        /// <summary>
        /// Shoko Group ID.
        /// </summary>
        public int ID;

        /// <summary>
        /// Series type.
        /// </summary>
        [Required]
        [JsonConverter(typeof(StringEnumConverter))]
        public SeriesType Type { get; set; }

        /// <summary>
        /// The overall rating from AniDB.
        /// </summary>
        public Rating Rating { get; set; }

        /// <summary>
        /// First aired date. Anything without an air date is going to be missing a lot of info.
        /// </summary>
        [Required]
        [JsonConverter(typeof(DateFormatConverter), "yyyy-MM-dd")]
        public DateTime? AirDate { get; set; }

        /// <summary>
        /// Last aired date. Will be null if the series is still ongoing.
        /// </summary>
        [JsonConverter(typeof(DateFormatConverter), "yyyy-MM-dd")]
        public DateTime? EndDate { get; set; }

        /// <summary>
        /// Tags for the main series.
        /// </summary>
        /// <value></value>
        public List<Tag> Tags { get; set; }
    }

    public class WebUISeriesExtra
    {
        /// <summary>
        /// The first season this show was aired in.
        /// </summary>
        /// <value></value>
        public Filter FirstAirSeason { get; set; }

        /// <summary>
        /// A pre-filtered list of studios for the show.
        /// </summary>
        public List<Role.Person> Studios { get; set; }

        /// <summary>
        /// A pre-filtered list of producers for the show.
        /// </summary>
        /// <value></value>
        public List<Role.Person> Producers { get; set; }

        /// <summary>
        /// The inferred source material for the series.
        /// </summary>
        public string SourceMaterial { get; set; }

        public WebUISeriesExtra(HttpContext ctx, SVR_AnimeSeries series)
        {
            var anime = series.GetAnime();
            var cast = Series.GetCast(anime.AnimeID, new () { Role.CreatorRoleType.Studio, Role.CreatorRoleType.Producer });

            FirstAirSeason = GetFirstAiringSeasonGroupFilter(ctx, anime);
            Studios = cast
                .Where(role => role.RoleName == Role.CreatorRoleType.Studio)
                .Select(role => role.Staff)
                .ToList();
            Producers = cast
                .Where(role => role.RoleName == Role.CreatorRoleType.Producer)
                .Select(role => role.Staff)
                .ToList();
            SourceMaterial = Series.GetTags(anime, TagFilter.Filter.Invert | TagFilter.Filter.Source, excludeDescriptions: true)
                .FirstOrDefault()?.Name ?? "Original Work";
        }

        private Filter GetFirstAiringSeasonGroupFilter(HttpContext ctx, SVR_AniDB_Anime anime)
        {
            var type = (AnimeType)anime.AnimeType;
            if (type != AnimeType.TVSeries && type != AnimeType.Web)
                return null;

            var (year, season) = anime.GetSeasons()
                .FirstOrDefault();
            if (year == 0)
                return null;

            var seasonName = $"{season} {year}";
            var seasonsFilterID = RepoFactory.GroupFilter.GetTopLevel()
                .FirstOrDefault(f => f.GroupFilterName == "Seasons").GroupFilterID;
            var firstAirSeason = RepoFactory.GroupFilter.GetByParentID(seasonsFilterID)
                .FirstOrDefault(f => f.GroupFilterName == seasonName);
            if (firstAirSeason == null)
                return null;

            return new Filter(ctx, firstAirSeason);
        } 
    }

    public class WebUISeriesFileSummary
    {
        public WebUISeriesFileSummary(SVR_AnimeSeries series)
        {
            var crossRefs = RepoFactory.CrossRef_File_Episode
                .GetByAnimeID(series.AniDB_ID);
            // The episodes we want to look at. We filter it down to only normal and speical episodes.
            var episodes = series.GetAnimeEpisodes()
                .Select(shoko =>
                {
                    var anidb = shoko.AniDB_Episode;
                    var type = Episode.MapAniDBEpisodeType(anidb.GetEpisodeTypeEnum());
                    var airDate = anidb.GetAirDateAsDate();
                    return new
                    {
                        ID = shoko.AniDB_EpisodeID,
                        Type = type,
                        Number = anidb.EpisodeNumber,
                        AirDate = airDate,
                        IsHidden = shoko.IsHidden,
                        Shoko = shoko,
                        AniDB = anidb,
                    };
                })
                .Where(episode => episode.Type == EpisodeType.Normal || episode.Type == EpisodeType.Special || episode.Type == EpisodeType.Unknown)
                .ToDictionary(episode => episode.ID);
            var anidbFiles = crossRefs
                // We only check for the file if the source is anidb.
                .Select(xref => (CrossRefSource)xref.CrossRefSource == CrossRefSource.AniDB ? RepoFactory.AniDB_File.GetByHash(xref.Hash) : null)
                .Where(anidbFile => anidbFile != null)
                // Multiple cross-references can be linked to the same anidb file.
                .DistinctBy(anidbFile => anidbFile.Hash)
                .ToDictionary(anidbFile => anidbFile.Hash);
            var releaseGroups = anidbFiles.Values
                .Select(anidbFile => anidbFile.AniDB_FileID)
                .Distinct()
                .Where(groupID => groupID != 0)
                .Select(groupID =>  RepoFactory.AniDB_ReleaseGroup.GetByGroupID(groupID))
                .Where(releaseGroup => releaseGroup != null)
                .ToDictionary(releaseGroup => releaseGroup.GroupID);
            // We only care about files with exist and have actual media info and with an actual physical location. (Which should hopefully exclude nothing.)
            var files = crossRefs
                .Where(xref => episodes.ContainsKey(xref.EpisodeID))
                .Select(xref =>
                {
                    var file = RepoFactory.VideoLocal.GetByHash(xref.Hash);
                    var location = file?.GetBestVideoLocalPlace();
                    if (file?.Media == null || location == null)
                        return null;

                    var media = new MediaInfo(file, file.Media);
                    var episode = episodes[xref.EpisodeID];
                    var isAutoLinked = (CrossRefSource)xref.CrossRefSource == CrossRefSource.AniDB;
                    var anidbFile = isAutoLinked && anidbFiles.ContainsKey(xref.Hash) ? anidbFiles[xref.Hash] : null;
                    var releaseGroup = anidbFile != null && anidbFile.GroupID != 0 && releaseGroups.ContainsKey(anidbFile.GroupID) ? releaseGroups[anidbFile.GroupID] : null;

                    var dirPath = System.IO.Path.GetDirectoryName(location.FullServerPath);
                    var groupName = isAutoLinked ? file.ReleaseGroup?.GroupName ?? "Unknown" : "None";
                    var version = isAutoLinked ? anidbFile?.FileVersion ?? 1 : 1;
                    var source = File.ParseFileSource(anidbFile?.File_Source);
                    var videoStream = media.Video.FirstOrDefault();
                    var bitDepth = videoStream.BitDepth;
                    var width = videoStream.Width;
                    var height = videoStream.Height;
                    var resolution = MediaInfoUtils.GetStandardResolution(new(width, height));
                    var videoCodecs = string.Join(", ", media.Video
                        .Select(stream => stream.Codec.Simplified)
                        .Distinct()
                        .OrderBy(codec => codec)
                        .ToList());
                    var audioCodecs = string.Join(", ", media.Audio
                        .Select(stream => stream.Codec.Simplified)
                        .Distinct()
                        .OrderBy(codec => codec)
                        .ToList());
                    var subtitleCodecs = string.Join(", ", media.Subtitles
                        .Select(stream => stream.Codec.Simplified)
                        .Distinct()
                        .OrderBy(codec => codec)
                        .ToList());
                    var subtitleLanguage = string.Join(", ", media.Subtitles
                        .Select(stream => stream.LanguageCode)
                        .Where(language => language != null)
                        .Distinct()
                        .OrderBy(language => language)
                        .ToList());
                    var audioLanguage = string.Join(", ", media.Audio
                        .Select(stream => stream.LanguageCode)
                        .Where(language => language != null)
                        .Distinct()
                        .OrderBy(language => language)
                        .ToList());
                    return new
                    {
                        Hash = xref.Hash,
                        FileID = file.VideoLocalID,
                        EpisodeID = episode.ID,
                        GroupBy = new {
                            GroupName = groupName,
                            Version = version,
                            Source = source,
                            BitDepth = bitDepth,
                            Resolution = resolution,
                            Width = width,
                            Height = height,
                            VideoCodecs = videoCodecs,
                            AudioCodecs = audioCodecs,
                            AudioLanguage = audioLanguage,
                            SubtitleCodecs = subtitleCodecs,
                            SubtitleLanguage = subtitleLanguage,
                            Location = dirPath,
                        },
                        Value = new {
                            Type = episode.Type,
                            Number = episode.Number,
                            Size = file.FileSize,
                        },
                    };
                })
                .Where(file => file != null)
                .ToList();
            var presentEpisodes = files
                .Select(xref => episodes[xref.EpisodeID])
                .DistinctBy(episode => episode.ID)
                .ToDictionary(episode => episode.ID);
            Ranges = files
                .GroupBy(data => data.GroupBy, data => data.Value)
                .Select(list =>
                {
                    var data = list.Key;
                    var episodeData = list
                        .GroupBy(ep => ep.Type)
                        .ToDictionary(
                            data => data.Key,
                            data =>
                            {
                                var episodes = data.ToList();
                                var sequence = data
                                    .Select(file => file.Number)
                                    .Distinct()
                                    .ToList();
                                var range = SequenceToRange(sequence);
                                var fileSize = data
                                    .Select(file => file.Size)
                                    .Sum();
                                return new EpisodeRangeByType {
                                    Range = range,
                                    FileSize = fileSize,
                                };
                            }
                        );
                    return new EpisodeRangeSummary()
                    {
                        GroupName = data.GroupName,
                        Version = data.Version,
                        Source = data.Source,
                        BitDepth = data.BitDepth,
                        Resolution = data.Resolution,
                        Width = data.Width,
                        Height = data.Height,
                        VideoCodecs = data.VideoCodecs,
                        AudioCodecs = data.AudioCodecs,
                        SubtitleCodecs = data.SubtitleCodecs,
                        AudioLanguage = data.AudioLanguage,
                        SubtitleLanguage = data.SubtitleLanguage,
                        Location = data.Location,
                        RangeByType = episodeData,
                    };
                })
                .ToList();

            MissingEpisodes = episodes.Values
                .Where(episode => !presentEpisodes.ContainsKey(episode.ID) && episode.AirDate != null && !episode.IsHidden)
                .OrderBy(episode => episode.Type)
                .ThenBy(episode => episode.Number)
                .Select(episode => new Episode.AniDB(episode.AniDB))
                .ToList();
        }

        public List<EpisodeRangeSummary> Ranges;

        public List<Episode.AniDB> MissingEpisodes;

        /// <summary>
        /// Summary of an episode range.
        /// </summary>
        public class EpisodeRangeSummary
        {
            /// <summary>
            /// The release group for the files in this range. Will be "Unknown"
            /// if the release group is unknown, or "None" if manually linked.
            /// </summary>
            public string GroupName;

            /// <summary>
            /// The release version for the files in this range.
            /// </summary>
            public int Version;

            /// <summary>
            /// The source type for the files in this range (e.g., Blu-ray, WEB-DL, etc.).
            /// </summary>
            public FileSource Source;

            /// <summary>
            /// The bit depth for the video stream of the files in this range.
            /// </summary>
            public int BitDepth;

            /// <summary>
            /// The common name of the resolution for the files in this range (e.g., 720p, 1080p, etc.).
            /// </summary>
            public string Resolution;

            /// <summary>
            /// The viewport width for the video stream of the files in this range.
            /// </summary>
            public int Width;

            /// <summary>
            /// The viewport height for the video stream of the files in this range.
            /// </summary>
            public int Height;

            /// <summary>
            /// The video codecs used in the files of this range (e.g., H.264, H.265, etc.).
            /// </summary>
            public string VideoCodecs;

            /// <summary>
            /// The audio codecs used in the files of this range (e.g., AAC, AC3, DTS, etc.).
            /// </summary>
            public string AudioCodecs;

            /// <summary>
            /// The ISO 639-1 two-letter language code for the audio language of the files in this range (e.g., "en" for English, "ja" for Japanese, etc.).
            /// </summary>
            public string AudioLanguage;

            /// <summary>
            /// The subtitle/text codecs used in the files of this range (e.g., SRT, VobSub, etc.).
            /// </summary>
            public string SubtitleCodecs;

            /// <summary>
            /// The ISO 639-1 two-letter language code for the subtitle/text language of the files in this range (e.g., "en" for English, "ja" for Japanese, etc.).
            /// </summary>
            public string SubtitleLanguage;

            /// <summary>
            /// The parent directory location of the files in this range.
            /// </summary>
            public string Location;

            /// <summary>
            /// Dictionary of episode ranges and sizes by type (e.g., normal episode, special episode).
            /// </summary>
            public Dictionary<EpisodeType, EpisodeRangeByType> RangeByType;
        }

        /// <summary>
        /// Represents the episode range and size by type.
        /// </summary>
        public class EpisodeRangeByType
        {
            /// <summary>
            /// The episode range included in this summary.
            /// </summary>
            /// <example>
            /// "01-03, 05, 07-134, 342-432"
            /// </example>
            public string Range;

            /// <summary>
            /// The accumulated file size in bytes across all files in this range.
            /// </summary>
            public long FileSize;
        }

        /// <summary>
        /// Converts a list of episode numbers into a range format string.
        /// </summary>
        /// <param name="sequence">The list of episode numbers to convert.</param>
        /// <returns>A range format string representing the given episode numbers.</returns>
        private static string SequenceToRange(List<int> sequence)
        {
            if (sequence == null || sequence.Count == 0)
                return "";

            if (sequence.Count == 1)
                return sequence[0].ToString().PadLeft(2, '0');

            var list = sequence.Distinct().OrderBy(x => x).ToList();
            var ranges = new List<string>();
            int start = list[0], end = list[0];
            for (int i = 1; i < list.Count; i++)
            {
                if (list[i] == end + 1)
                {
                    end = list[i];
                }
                else
                {
                    var range = start == end
                        ? start.ToString().PadLeft(2, '0')
                        : $"{start.ToString().PadLeft(2, '0')}-{end.ToString().PadLeft(2, '0')}";
                    ranges.Add(range);
                    start = end = list[i];
                }
            }
            var finalRange = start == end
                ? start.ToString().PadLeft(2, '0')
                : $"{start.ToString().PadLeft(2, '0')}-{end.ToString().PadLeft(2, '0')}";
            ranges.Add(finalRange);
            return string.Join(", ", ranges);
        }

    }

    public class Input
    {
        public class WebUIGroupViewBody
        {
            /// <summary>
            /// Group IDs to fetch info for.
            /// </summary>
            /// <value></value>
            [Required]
            [MaxLength(100)]
            public HashSet<int> GroupIDs { get; set; }

            /// <summary>
            /// Tag filter.
            /// </summary>
            /// <value></value>
            public TagFilter.Filter TagFilter { get; set; } = 0;

            /// <summary>
            /// Limits the number of returned tags.
            /// </summary>
            /// <value></value>
            public int TagLimit { get; set; } = 30;

            /// <summary>
            /// Order tags by name (and source) only. Don't use the tag weights.
            /// </summary>
            /// <value></value>
            public bool OrderByName { get; set; } = false;
        }
    }
}
