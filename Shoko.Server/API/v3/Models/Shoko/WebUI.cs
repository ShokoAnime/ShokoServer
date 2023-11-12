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
using Shoko.Server.API.WebUI;
using Shoko.Server.Models;
using Shoko.Server.Repositories;

using AnimeType = Shoko.Models.Enums.AnimeType;

namespace Shoko.Server.API.v3.Models.Shoko;

public class WebUI
{
    public class WebUITheme
    {
        /// <summary>
        /// The theme id is inferred from the filename of the theme definition file.
        /// </summary>
        /// <remarks>
        /// Only JSON-files with an alphanumerical filename will be checked if they're themes. All other files will be skipped outright.
        /// </remarks>
        public readonly string ID;

        /// <summary>
        /// The display name of the theme.
        /// </summary>
        public readonly string Name;

        /// <summary>
        /// A short description about the theme, if available.
        /// </summary>
        public readonly string Description;

        /// <summary>
        /// The name of the author of the theme definition.
        /// </summary>
        public readonly string Author;

        /// <summary>
        /// Indicates this is only a preview of the theme metadata and the theme
        /// might not actaully be installed yet.
        /// </summary>
        public readonly bool IsPreview;

        /// <summary>
        /// Indicates the theme is installed locally.
        /// </summary>
        public readonly bool IsInstalled;

        /// <summary>
        /// The theme version.
        /// </summary>
        public readonly Version Version;

        /// <summary>
        /// Author-defined tags assosiated with the theme.
        /// </summary>
        public readonly IReadOnlyList<string> Tags;

        /// <summary>
        /// The URL for where the theme definition lives. Used for updates.
        /// </summary>
        public readonly string URL;
        
        /// <summary>
        /// The CSS representation of the theme.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public readonly string CSS;

        public WebUITheme(WebUIThemeProvider.ThemeDefinition definition, bool withCSS = false)
        {
            ID = definition.ID;
            Name = definition.Name;
            Description = definition.Description;
            Tags = definition.Tags;
            Author = definition.Author;
            Version = definition.Version;
            URL = definition.URL;
            IsPreview = definition.IsPreview;
            IsInstalled = definition.IsInstalled;
            CSS = withCSS ? definition.ToCSS() : null;
        }
    }

    public class WebUIGroupExtra
    {
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
    }

    public class WebUISeriesFileSummary
    {
        public WebUISeriesFileSummary(SVR_AnimeSeries series, HashSet<EpisodeType> episodeTypes = null, bool withEpisodeDetails = false, bool showMissingFutureEpisodes = false, bool showMissingUnknownEpisodes = false, HashSet<FileSummaryGroupByCriteria> groupByCriteria = null)
        {
            // By default only show 'normal', 'special' or 'other' episodes.
            episodeTypes ??= new() { EpisodeType.Normal, EpisodeType.Special, EpisodeType.Other };
            // By default, don't divide into groups.
            groupByCriteria ??= new();
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
                // Show everything if no types are provided, otherwise filter to the given types.
                .Where(episode => episodeTypes.Count > 0 ? episodeTypes.Contains(episode.Type) : true)
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
                    var groupByDetails = new GroupByDetails();

                    // Release group criterias
                    if (groupByCriteria.Contains(FileSummaryGroupByCriteria.GroupName))
                    {
                        groupByDetails.GroupName = isAutoLinked ? file.ReleaseGroup?.GroupName ?? "Unknown" : "None";
                        groupByDetails.GroupNameShort = isAutoLinked ? file.ReleaseGroup?.GroupNameShort ?? "Unk" : "-";
                    }

                    // File criterias
                    if (groupByCriteria.Contains(FileSummaryGroupByCriteria.FileVersion))
                        groupByDetails.FileVersion = isAutoLinked ? anidbFile?.FileVersion ?? 1 : 1;
                    if (groupByCriteria.Contains(FileSummaryGroupByCriteria.FileSource))
                        groupByDetails.FileSource = File.ParseFileSource(anidbFile?.File_Source);
                    if (groupByCriteria.Contains(FileSummaryGroupByCriteria.FileLocation))
                        groupByDetails.FileLocation = System.IO.Path.GetDirectoryName(location.FullServerPath);

                    // Video criterias
                    if (groupByCriteria.Contains(FileSummaryGroupByCriteria.VideoCodecs))
                        groupByDetails.VideoCodecs = string.Join(", ", media.Video
                            .Select(stream => stream.Codec.Simplified)
                            .Distinct()
                            .OrderBy(codec => codec)
                            .ToList());
                    if (groupByCriteria.Contains(FileSummaryGroupByCriteria.VideoBitDepth) ||
                        groupByCriteria.Contains(FileSummaryGroupByCriteria.VideoResolutuion))
                    {
                        var videoStream = media.Video.FirstOrDefault();
                        if (groupByCriteria.Contains(FileSummaryGroupByCriteria.VideoBitDepth))
                            groupByDetails.VideoBitDepth = videoStream?.BitDepth ?? 0;
                        if (groupByCriteria.Contains(FileSummaryGroupByCriteria.VideoBitDepth))
                        {
                            var width = videoStream?.Width ?? 0;
                            var height = videoStream?.Height ?? 0;
                            groupByDetails.VideoWidth = width;
                            groupByDetails.VideoHeight = height;
                            groupByDetails.VideoResolution = MediaInfoUtils.GetStandardResolution(new(width, height));
                        }
                    }

                    // Audio criterias
                    if (groupByCriteria.Contains(FileSummaryGroupByCriteria.AudioCodecs))
                        groupByDetails.AudioCodecs = string.Join(", ", media.Audio
                            .Select(stream => stream.Codec.Simplified)
                            .Distinct()
                            .OrderBy(codec => codec)
                            .ToList());
                    if (groupByCriteria.Contains(FileSummaryGroupByCriteria.AudioLanguages))
                        groupByDetails.AudioLanguages = string.Join(", ", media.Audio
                            .Select(stream => stream.LanguageCode ?? "unk")
                            .Distinct()
                            .OrderBy(language => language)
                            .ToList());
                    if (groupByCriteria.Contains(FileSummaryGroupByCriteria.AudioStreamCount))
                        groupByDetails.AudioStreamCount = media.Audio.Count;

                    // Text criterias
                    if (groupByCriteria.Contains(FileSummaryGroupByCriteria.SubtitleCodecs))
                        groupByDetails.SubtitleCodecs = string.Join(", ", media.Subtitles
                            .Select(stream => stream.Codec.Simplified)
                            .Distinct()
                            .OrderBy(codec => codec)
                            .ToList());
                    if (groupByCriteria.Contains(FileSummaryGroupByCriteria.SubtitleLanguages))
                        groupByDetails.SubtitleLanguages = string.Join(", ", media.Subtitles
                            .Select(stream => stream.LanguageCode ?? "unk")
                            .Distinct()
                            .OrderBy(language => language)
                            .ToList());
                    if (groupByCriteria.Contains(FileSummaryGroupByCriteria.SubtitleStreamCount))
                        groupByDetails.SubtitleStreamCount = media.Subtitles.Count;

                    return new
                    {
                        GroupBy = groupByDetails,
                        Episode = new EpisodeDetails
                        {
                            FileID = anidbFile?.FileID,
                            EpisodeID = episode.AniDB.EpisodeID,
                            Type = episode.Type,
                            Number = episode.Number,
                            Size = file.FileSize,
                            ED2K = file.Hash,
                        },
                    };
                })
                .Where(file => file != null)
                .ToList();
            var presentEpisodes = files
                .Select(xref => xref.Episode.EpisodeID)
                .ToHashSet();
            Groups = files
                .GroupBy(data => data.GroupBy, data => data.Episode)
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
                                    Count = sequence.Count,
                                    Range = range,
                                    FileSize = fileSize,
                                };
                            }
                        );
                    return new EpisodeGroupSummary()
                    {
                        GroupName = data.GroupName,
                        GroupNameShort = data.GroupNameShort,
                        FileVersion = data.FileVersion,
                        FileSource = data.FileSource,
                        FileLocation = data.FileLocation,
                        VideoCodecs = data.VideoCodecs,
                        VideoBitDepth = data.VideoBitDepth,
                        VideoResolution = data.VideoResolution,
                        VideoWidth = data.VideoWidth,
                        VideoHeight = data.VideoHeight,
                        AudioCodecs = data.AudioCodecs,
                        AudioLanguages = data.AudioLanguages == null ? null :
                            string.IsNullOrEmpty(data.AudioLanguages) ? new string[] {} : data.AudioLanguages.Split(", "),
                        AudioStreamCount = data.AudioStreamCount,
                        SubtitleCodecs = data.SubtitleCodecs,
                        SubtitleLanguages = data.SubtitleLanguages == null ? null :
                            string.IsNullOrEmpty(data.SubtitleLanguages) ? new string[] {} : data.SubtitleLanguages.Split(", "),
                        SubtitleStreamCount = data.SubtitleStreamCount,
                        RangeByType = episodeData,
                        Episodes = withEpisodeDetails ? list
                            .OrderBy(ep => ep.Type)
                            .ThenBy(ep => ep.Number)
                            .ThenBy(ep => ep.ED2K)
                            .ToList() : null,
                    };
                })
                .ToList();

            var now = DateTime.Now;
            MissingEpisodes = episodes.Values
                .Where(episode => !presentEpisodes.Contains(episode.ID) && !episode.IsHidden && (episode.AirDate.HasValue ? (showMissingFutureEpisodes || episode.AirDate.Value < now) : showMissingUnknownEpisodes))
                .OrderBy(episode => episode.Type)
                .ThenBy(episode => episode.Number)
                .Select(episode => new Episode.AniDB(episode.AniDB))
                .ToList();
        }

        public List<EpisodeGroupSummary> Groups;

        public List<Episode.AniDB> MissingEpisodes;

        [JsonConverter(typeof(StringEnumConverter))]
        public enum FileSummaryGroupByCriteria
        {
            GroupName = 1,
            FileVersion = 2,
            FileSource = 4,
            FileLocation = 8,
            VideoCodecs = 16,
            VideoBitDepth = 32,
            VideoResolutuion = 64,
            AudioCodecs = 128,
            AudioLanguages = 256,
            AudioStreamCount = 512,
            SubtitleCodecs = 1024,
            SubtitleLanguages = 2048,
            SubtitleStreamCount = 4096,
        }

        /// <summary>
        /// Summary of a group of episodes.
        /// </summary>
        public class EpisodeGroupSummary
        {
            /// <summary>
            /// The name release group for the files in this range. Will be
            /// "Unknown" if the release group is unknown, or "None" if manually
            /// linked.
            /// </summary>
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public string GroupName;

            /// <summary>
            /// The short name of the release group for the files in this range.
            /// Will be "Unk" if the release group is unknown, or "-" if
            /// manually linked.
            /// </summary>
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public string GroupNameShort;

            /// <summary>
            /// The release version for the files in this range.
            /// </summary>
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public int? FileVersion;

            /// <summary>
            /// The source type for the files in this range (e.g., BluRay, Web, etc.).
            /// </summary>
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public FileSource? FileSource;

            /// <summary>
            /// The parent directory location of the files in this range.
            /// </summary>
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public string FileLocation;

            /// <summary>
            /// The video codecs used in the files of this range (e.g., h264, h265, etc.).
            /// </summary>
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public string VideoCodecs;

            /// <summary>
            /// The bit depth for the video stream of the files in this range.
            /// </summary>
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public int? VideoBitDepth;

            /// <summary>
            /// The common name of the resolution for the files in this range (e.g., 720p, 1080p, etc.).
            /// </summary>
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public string VideoResolution;

            /// <summary>
            /// The viewport width for the video stream of the files in this range.
            /// </summary>
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public int? VideoWidth;

            /// <summary>
            /// The viewport height for the video stream of the files in this range.
            /// </summary>
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public int? VideoHeight;

            /// <summary>
            /// The audio codecs used in the files of this range (e.g., acc, ac3, dts, etc.).
            /// </summary>
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public string AudioCodecs;

            /// <summary>
            /// The ISO 639-1 two-letter language codes for the audio streams of the files in this range (e.g., "en" for English, "ja" for Japanese, etc.).
            /// </summary>
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public IEnumerable<string> AudioLanguages;

            /// <summary>
            /// The number of audio streams in the files in the range.
            /// </summary>
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public int? AudioStreamCount;

            /// <summary>
            /// The subtitle/text codecs used in the files of this range (e.g., srt, ass, etc.).
            /// </summary>
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public string SubtitleCodecs;

            /// <summary>
            /// The ISO 639-1 two-letter language codes for the subtitle/text streams of the files in this range (e.g., "en" for English, "ja" for Japanese, etc.).
            /// </summary>
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public IEnumerable<string> SubtitleLanguages;

            /// <summary>
            /// The number of subtitle/text streams in the files in the range.
            /// </summary>
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public int? SubtitleStreamCount;

            /// <summary>
            /// Dictionary of episode ranges and sizes by type (e.g., normal episode, special episode).
            /// </summary>
            public Dictionary<EpisodeType, EpisodeRangeByType> RangeByType;

            /// <summary>
            /// Episodes in the group.
            /// </summary>
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public IEnumerable<EpisodeDetails> Episodes;
        }

        /// <summary>
        /// Represents the episode range and size by type.
        /// </summary>
        public class EpisodeRangeByType
        {
            /// <summary>
            /// Total number of episodes for the type in the range.
            /// </summary>
            public int Count;

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

        private class GroupByDetails : IEquatable<GroupByDetails>
        {
            public string GroupName;
            public string GroupNameShort;
            public int? FileVersion;
            public FileSource? FileSource;
            public int? VideoBitDepth;
            public string VideoResolution;
            public int? VideoWidth;
            public int? VideoHeight;
            public string VideoCodecs;
            public string AudioCodecs;
            public string AudioLanguages;
            public int? AudioStreamCount;
            public string SubtitleCodecs;
            public string SubtitleLanguages;
            public int? SubtitleStreamCount;
            public string FileLocation;

            public bool Equals(GroupByDetails other)
            {
                if (other == null)
                    return false;
                return 
                    GroupName == other.GroupName &&
                    // GroupNameShort == other.GroupNameShort &&

                    FileVersion == other.FileVersion &&
                    FileSource == other.FileSource &&
                    FileLocation == other.FileLocation &&

                    VideoCodecs == other.VideoCodecs &&
                    VideoBitDepth == other.VideoBitDepth &&
                    VideoResolution == other.VideoResolution &&
                    // VideoWidth == other.VideoWidth &&
                    // VideoHeight == other.VideoHeight &&

                    AudioCodecs == other.AudioCodecs &&
                    AudioLanguages == other.AudioLanguages &&
                    AudioStreamCount == other.AudioStreamCount &&

                    SubtitleCodecs == other.SubtitleCodecs &&
                    SubtitleLanguages == other.SubtitleLanguages &&
                    SubtitleStreamCount == other.SubtitleStreamCount;
            }

            public override int GetHashCode()
            {
                int hash = 17;

                hash = hash * 31 + (GroupName?.GetHashCode() ?? 0);
                // hash = hash * 31 + (GroupNameShort?.GetHashCode() ?? 0);

                hash = hash * 31 + FileVersion.GetHashCode();
                hash = hash * 31 + FileSource.GetHashCode();
                hash = hash * 31 + (FileLocation?.GetHashCode() ?? 0);

                hash = hash * 31 + (VideoCodecs?.GetHashCode() ?? 0);
                hash = hash * 31 + VideoBitDepth.GetHashCode();
                hash = hash * 31 + (VideoResolution?.GetHashCode() ?? 0);
                // hash = hash * 31 + VideoWidth.GetHashCode();
                // hash = hash * 31 + VideoHeight.GetHashCode();

                hash = hash * 31 + (AudioCodecs?.GetHashCode() ?? 0);
                hash = hash * 31 + (AudioLanguages?.GetHashCode() ?? 0);
                hash = hash * 31 + AudioStreamCount.GetHashCode();

                hash = hash * 31 + (SubtitleCodecs?.GetHashCode() ?? 0);
                hash = hash * 31 + (SubtitleLanguages?.GetHashCode() ?? 0);
                hash = hash * 31 + SubtitleStreamCount.GetHashCode();

                return hash;
            }
        }

        public class EpisodeDetails
        {
            /// <summary>
            /// AniDB Episode ID.
            /// /// </summary>
            public int EpisodeID;

            /// <summary>
            /// AniDB File ID, if auto-linked.
            /// </summary>
            public int? FileID;

            /// <summary>
            /// Episode Type.
            /// </summary>
            [JsonConverter(typeof(StringEnumConverter))]
            public EpisodeType Type;

            /// <summary>
            /// Episode Number.
            /// </summary>
            public int Number;

            /// <summary>
            /// File Size.
            /// </summary>
            public long Size;

            /// <summary>
            /// ED2K File Hash.
            /// </summary>
            public string ED2K;
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

        /// <summary>
        /// Represents the request body for adding or previewing a theme in the Web UI.
        /// </summary>
        public class WebUIAddThemeBody
        {
            /// <summary>
            /// Gets or sets the URL from where to retrieve the theme.
            /// </summary>
            [Required]
            [Url]
            public string URL { get; set; }

            /// <summary>
            /// Gets or sets a flag indicating whether to enable preview mode for the theme.
            /// If true, the theme will be previewed without being added permanently.
            /// If false, the theme will be added as a permanent option in the Web UI.
            /// </summary>
            public bool Preview { get; set; } = false;
        }
    }
}
