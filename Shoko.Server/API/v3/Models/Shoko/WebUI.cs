using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
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

#nullable enable
namespace Shoko.Server.API.v3.Models.Shoko;

public class WebUI
{
    public class WebUITheme(WebUIThemeProvider.ThemeDefinition definition, bool withCSS = false)
    {
        /// <summary>
        /// The theme id is inferred from the filename of the theme definition file.
        /// </summary>
        /// <remarks>
        /// Only JSON-files with an alphanumerical filename will be checked if they're themes. All other files will be skipped outright.
        /// </remarks>
        public string ID { get; init; } = definition.ID;

        /// <summary>
        /// The display name of the theme.
        /// </summary>
        public string Name { get; init; } = definition.Name;

        /// <summary>
        /// A short description about the theme, if available.
        /// </summary>
        public string? Description { get; init; } = definition.Description;

        /// <summary>
        /// The name of the author of the theme definition.
        /// </summary>
        public string Author { get; init; } = definition.Author ?? "<unknown>";

        /// <summary>
        /// Indicates this is only a preview of the theme metadata and the theme
        /// might not actually be installed yet.
        /// </summary>
        public bool IsPreview { get; init; } = definition.IsPreview;

        /// <summary>
        /// Indicates the theme is installed locally.
        /// </summary>
        public bool IsInstalled { get; init; } = definition.IsInstalled;

        /// <summary>
        /// The theme version.
        /// </summary>
        public Version Version { get; init; } = definition.Version;

        /// <summary>
        /// Author-defined tags associated with the theme.
        /// </summary>
        public IReadOnlyList<string> Tags { get; init; } = definition.Tags;

        /// <summary>
        /// The URL for where the theme definition lives. Used for updates.
        /// </summary>
        public string? URL { get; init; } = definition.UpdateUrl;

        /// <summary>
        /// The CSS representation of the theme.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? CSS { get; init; } = withCSS ? definition.ToCSS() : null;
    }

    public class WebUIGroupExtra
    {
        /// <summary>
        /// Shoko Group ID.
        /// </summary>
        public int ID { get; set; }

        /// <summary>
        /// Series type.
        /// </summary>
        [Required]
        [JsonConverter(typeof(StringEnumConverter))]
        public SeriesType Type { get; set; }

        /// <summary>
        /// The overall rating from AniDB.
        /// </summary>
        public Rating Rating { get; set; } = new();

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
        public List<Tag> Tags { get; set; } = [];
    }

    public class WebUISeriesExtra
    {
        /// <summary>
        /// Common runtime length for the episodes, if the series have any
        /// episodes.
        /// </summary>
        public TimeSpan? RuntimeLength { get; set; }

        /// <summary>
        /// The first season this show was aired in.
        /// </summary>
        /// <value></value>
        public string? FirstAirSeason { get; set; }

        /// <summary>
        /// A pre-filtered list of studios for the show.
        /// </summary>
        public List<Role.Person> Studios { get; set; } = [];

        /// <summary>
        /// A pre-filtered list of producers for the show.
        /// </summary>
        /// <value></value>
        public List<Role.Person> Producers { get; set; } = [];

        /// <summary>
        /// The inferred source material for the series.
        /// </summary>
        public string SourceMaterial { get; set; } = string.Empty;
    }

    public class WebUISeriesFileSummary
    {
        public WebUISeriesFileSummary(
            SVR_AnimeSeries series,
            HashSet<EpisodeType>? episodeTypes = null,
            bool withEpisodeDetails = false,
            bool withLocationDetails = false,
            bool showMissingFutureEpisodes = false,
            bool showMissingUnknownEpisodes = false,
            HashSet<FileSummaryGroupByCriteria>? groupByCriteria = null
        )
        {
            // By default only show 'normal', 'special' or 'other' episodes.
            episodeTypes ??= [EpisodeType.Normal, EpisodeType.Special, EpisodeType.Other];
            // By default, don't divide into groups.
            groupByCriteria ??= [];
            var now = DateTime.Now;
            var crossRefs = RepoFactory.CrossRef_File_Episode.GetByAnimeID(series.AniDB_ID)
                .Select(xref => (xref, video: xref.VideoLocal!))
                .Where(tuple => tuple.video is not null)
                .ToList();
            // The episodes we want to look at. We filter it down to only normal and special episodes.
            var episodes = series.AllAnimeEpisodes
                .Select(shoko =>
                {
                    var anidb = shoko.AniDB_Episode!;
                    var type = Episode.MapAniDBEpisodeType(anidb.GetEpisodeTypeEnum());
                    var airDate = anidb.GetAirDateAsDate();
                    return new
                    {
                        ID = shoko.AniDB_EpisodeID,
                        Type = type,
                        Number = anidb.EpisodeNumber,
                        AirDate = airDate,
                        Shoko = shoko,
                        AniDB = anidb,
                        shoko.IsHidden,
                    };
                })
                // Show everything if no types are provided, otherwise filter to the given types.
                .Where(episode => episodeTypes.Count == 0 || episodeTypes.Contains(episode.Type))
                .ToDictionary(episode => episode.ID);
            var anidbFiles = crossRefs
                // We only check for the file if the source is anidb.
                .Select(tuple => (CrossRefSource)tuple.xref.CrossRefSource == CrossRefSource.AniDB ? RepoFactory.AniDB_File.GetByHash(tuple.xref.Hash) : null)
                .WhereNotNull()
                // Multiple cross-references can be linked to the same anidb file.
                .DistinctBy(anidbFile => anidbFile.Hash)
                .ToDictionary(anidbFile => anidbFile.Hash);
            var releaseGroups = anidbFiles.Values
                .Select(anidbFile => anidbFile.GroupID)
                .Distinct()
                .Where(groupID => groupID != 0)
                .Select(RepoFactory.AniDB_ReleaseGroup.GetByGroupID)
                .WhereNotNull()
                .ToDictionary(releaseGroup => releaseGroup.GroupID);
            // We only care about files with exist and have actual media info and with an actual physical location. (Which should hopefully exclude nothing.)
            var filesWithXrefAndLocation = crossRefs
                .Where(tuple => episodes.ContainsKey(tuple.xref.EpisodeID))
                .SelectMany(tuple =>
                {
                    var (xref, file) = tuple;
                    if (file.MediaInfo is null)
                        return [];

                    if (groupByCriteria.Contains(FileSummaryGroupByCriteria.MultipleLocations))
                    {
                        var locations = file.Places;
                        if (locations.Count > 1)
                            return file.Places
                                .Select(location => (file, xref, location));

                        return [];
                    }

                    if (file.FirstValidPlace is { } firstLocation)
                        return [(file, xref, firstLocation)];

                    return [];
                })
                .ToList();
            var files = filesWithXrefAndLocation
                .Select(tuple =>
                {
                    var (file, xref, location) = tuple;
                    var media = new MediaInfo(file, file.MediaInfo!);
                    var episode = episodes[xref.EpisodeID];
                    var isAutoLinked = (CrossRefSource)xref.CrossRefSource == CrossRefSource.AniDB;
                    var anidbFile = isAutoLinked && anidbFiles.TryGetValue(xref.Hash, out var aniFi) ? aniFi : null;
                    var releaseGroup = anidbFile != null && anidbFile.GroupID != 0 && releaseGroups.TryGetValue(anidbFile.GroupID, out var reGr) ? reGr : null;
                    var groupByDetails = new GroupByDetails();

                    // Release group criteria
                    if (groupByCriteria.Contains(FileSummaryGroupByCriteria.GroupName))
                    {
                        groupByDetails.GroupName = isAutoLinked ? releaseGroup?.GroupName ?? "Unknown" : "None";
                        groupByDetails.GroupNameShort = isAutoLinked ? releaseGroup?.GroupNameShort ?? "Unk" : "-";
                    }

                    // File criteria
                    if (groupByCriteria.Contains(FileSummaryGroupByCriteria.FileVersion))
                        groupByDetails.FileVersion = isAutoLinked ? anidbFile?.FileVersion ?? 1 : 1;
                    if (groupByCriteria.Contains(FileSummaryGroupByCriteria.FileSource))
                        groupByDetails.FileSource = File.ParseFileSource(anidbFile?.File_Source);
                    if (groupByCriteria.Contains(FileSummaryGroupByCriteria.FileLocation))
                        groupByDetails.FileLocation = System.IO.Path.GetDirectoryName(location.FullServerPath)!;
                    if (groupByCriteria.Contains(FileSummaryGroupByCriteria.FileIsDeprecated))
                        groupByDetails.FileIsDeprecated = anidbFile?.IsDeprecated ?? false;
                    if (groupByCriteria.Contains(FileSummaryGroupByCriteria.ImportFolder))
                        groupByDetails.ImportFolder = location.ImportFolderID;
                    if (groupByCriteria.Contains(FileSummaryGroupByCriteria.ED2K))
                        groupByDetails.ED2K = $"{file.Hash}+{file.FileSize}";

                    // Video criteria
                    if (groupByCriteria.Contains(FileSummaryGroupByCriteria.VideoCodecs))
                        groupByDetails.VideoCodecs = string.Join(", ", media.Video
                            .Select(stream => stream.Codec.Simplified)
                            .Distinct()
                            .OrderBy(codec => codec)
                            .ToList());
                    if (groupByCriteria.Contains(FileSummaryGroupByCriteria.VideoBitDepth) ||
                        groupByCriteria.Contains(FileSummaryGroupByCriteria.VideoResolution))
                    {
                        var videoStream = media.Video.FirstOrDefault();
                        if (groupByCriteria.Contains(FileSummaryGroupByCriteria.VideoBitDepth))
                            groupByDetails.VideoBitDepth = videoStream?.BitDepth ?? 0;
                        if (groupByCriteria.Contains(FileSummaryGroupByCriteria.VideoResolution))
                        {
                            var width = videoStream?.Width ?? 0;
                            var height = videoStream?.Height ?? 0;
                            groupByDetails.VideoWidth = width;
                            groupByDetails.VideoHeight = height;
                            groupByDetails.VideoResolution = MediaInfoUtils.GetStandardResolution(new(width, height));
                        }
                    }
                    if (groupByCriteria.Contains(FileSummaryGroupByCriteria.VideoHasChapters))
                        groupByDetails.VideoHasChapters = media.Chapters.Count > 0;

                    // Audio criteria
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

                    // Text criteria
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
                            EpisodeID = episode.ID,
                            FileID = file.VideoLocalID,
                            Location = location,
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

            Overview = new()
            {
                TotalFileSize = files.Sum(fileWrapper => fileWrapper.Episode.Size),
                ReleaseGroups = releaseGroups.Values
                    .Select(group => group.GroupNameShort ?? group.GroupName)
                    .WhereNotNull()
                    .Distinct()
                    .ToList(),
                SourcesByType = filesWithXrefAndLocation
                    .Select(t =>
                    {
                        var episodeType = episodes[t.xref.EpisodeID].Type;
                        var fileSource = t.xref.CrossRefSource == (int)CrossRefSource.AniDB && anidbFiles.TryGetValue(t.xref.Hash, out var anidbFile)
                            ? File.ParseFileSource(anidbFile.File_Source) : FileSource.Unknown;

                        return (episodeType, fileSource);
                    })
                    .GroupBy(t => t.episodeType)
                    .Select(episodeTypeGroup =>
                    {
                        var sources = episodeTypeGroup
                            .GroupBy(tuple => tuple.fileSource)
                            .Select(sourceGroup => new SourceGrouping { Type = sourceGroup.Key, Count = sourceGroup.Count() })
                            .ToList();
                        return new SourcesByType { Type = episodeTypeGroup.Key, Sources = sources };
                    })
                    .ToList(),
            };
            Groups = files
                .GroupBy(tuple => tuple.GroupBy, data => data.Episode)
                .Select(list =>
                {
                    var details = list.Key;
                    var rangeByType = list
                        .GroupBy(episode => episode.Type)
                        .OrderBy(groupBy => groupBy.Key)
                        .ToDictionary(
                            groupBy => groupBy.Key,
                            groupBy =>
                            {
                                var data = groupBy.ToList();
                                var sequence = data
                                    .Select(file => file.Number)
                                    .Distinct()
                                    .ToList();
                                var range = SequenceToRange(sequence);
                                var fileSize = data.Sum(file => file.Size);
                                return new EpisodeRangeByType
                                {
                                    Count = sequence.Count,
                                    FirstEpisode = sequence.Min(),
                                    LastEpisode = sequence.Max(),
                                    Sequence = sequence,
                                    Range = range,
                                    FileSize = fileSize,
                                };
                            }
                        );
                    var sortByCriteria = rangeByType
                        .OrderBy(pair => pair.Key)
                        .Select(pair => new SortByCriteriaGroup(pair.Key, pair.Value.FirstEpisode, pair.Value.LastEpisode, pair.Value.Sequence))
                        .ToList();
                    return new EpisodeGroupSummary(rangeByType, new(sortByCriteria))
                    {
                        GroupName = details.GroupName,
                        GroupNameShort = details.GroupNameShort,
                        FileVersion = details.FileVersion,
                        FileSource = details.FileSource,
                        FileLocation = details.FileLocation,
                        FileIsDeprecated = details.FileIsDeprecated,
                        ImportFolder = details.ImportFolder,
                        ED2K = details.ED2K,
                        VideoCodecs = details.VideoCodecs,
                        VideoBitDepth = details.VideoBitDepth,
                        VideoResolution = details.VideoResolution,
                        VideoWidth = details.VideoWidth,
                        VideoHeight = details.VideoHeight,
                        VideoHasChapters = details.VideoHasChapters,
                        AudioCodecs = details.AudioCodecs,
                        AudioLanguages = details.AudioLanguages == null ? null :
                            string.IsNullOrEmpty(details.AudioLanguages) ? [] : details.AudioLanguages.Split(", "),
                        AudioStreamCount = details.AudioStreamCount,
                        SubtitleCodecs = details.SubtitleCodecs,
                        SubtitleLanguages = details.SubtitleLanguages == null ? null :
                            string.IsNullOrEmpty(details.SubtitleLanguages) ? [] : details.SubtitleLanguages.Split(", "),
                        SubtitleStreamCount = details.SubtitleStreamCount,
                        Episodes = withEpisodeDetails
                            ? list
                                .DistinctBy(ep => (ep.EpisodeID, ep.FileID))
                                .OrderBy(ep => ep.Type)
                                .ThenBy(ep => ep.Number)
                                .ThenBy(ep => ep.ED2K)
                                .ToList()
                            : null,
                        Locations = withLocationDetails
                            ? list.Select(episode => new File.Location(episode.Location, false))
                                .OrderBy(location => location.ImportFolderID)
                                .ThenBy(location => location.FileID)
                                .ThenBy(location => location.RelativePath)
                                .ToList()
                            : null,
                    };
                })
                .OrderBy(groupBy => groupBy.SortByCriteria)
                .ToList();
            MissingEpisodes = episodes.Values
                .Where(episode => !presentEpisodes.Contains(episode.ID) && !episode.IsHidden && (episode.AirDate.HasValue ? (showMissingFutureEpisodes || episode.AirDate.Value < now) : showMissingUnknownEpisodes))
                .OrderBy(episode => episode.Type)
                .ThenBy(episode => episode.Number)
                .Select(episode => new Episode.AniDB(episode.AniDB))
                .ToList();
        }

        public FileSummaryOverview Overview { get; set; }

        public List<EpisodeGroupSummary> Groups { get; set; }

        public List<Episode.AniDB> MissingEpisodes { get; set; }

        [JsonConverter(typeof(StringEnumConverter))]
        public enum FileSummaryGroupByCriteria
        {
            GroupName = 1,
            FileVersion = 2,
            FileSource = 4,
            FileLocation = 8,
            VideoCodecs = 16,
            VideoBitDepth = 32,
            VideoResolution = 64,
            AudioCodecs = 128,
            AudioLanguages = 256,
            AudioStreamCount = 512,
            SubtitleCodecs = 1024,
            SubtitleLanguages = 2048,
            SubtitleStreamCount = 4096,
            VideoHasChapters = 8192,
            FileIsDeprecated = 16384,
            ImportFolder = 32768,
            ED2K = 65536,
            MultipleLocations = 131072,
        }

        /// <summary>
        /// Summary of a group of episodes.
        /// </summary>
        public class EpisodeGroupSummary(Dictionary<EpisodeType, EpisodeRangeByType> rangeByType, SortByCriteria sortByCriteria)
        {
            /// <summary>
            /// The name release group for the files in this range. Will be
            /// "Unknown" if the release group is unknown, or "None" if manually
            /// linked.
            /// </summary>
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public string? GroupName { get; set; }

            /// <summary>
            /// The short name of the release group for the files in this range.
            /// Will be "Unk" if the release group is unknown, or "-" if
            /// manually linked.
            /// </summary>
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public string? GroupNameShort { get; set; }

            /// <summary>
            /// The release version for the files in this range.
            /// </summary>
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public int? FileVersion { get; set; }

            /// <summary>
            /// The source type for the files in this range (e.g., BluRay, Web, etc.).
            /// </summary>
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public FileSource? FileSource { get; set; }

            /// <summary>
            /// The parent directory location of the files in this range.
            /// </summary>
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public string? FileLocation { get; set; }

            /// <summary>
            /// Indicates that the file version is deprecated and has been super
            /// seeded by a newer file version by the same group.
            /// </summary>
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public bool? FileIsDeprecated { get; set; }

            /// <summary>
            /// The import folder name of the files in this range.
            /// </summary>
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public int? ImportFolder { get; set; }

            /// <summary>
            /// The ED2K hash + file size of the file in this range.
            /// </summary>
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public string? ED2K { get; set; }

            /// <summary>
            /// The video codecs used in the files of this range (e.g., h264, h265, etc.).
            /// </summary>
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public string? VideoCodecs { get; set; }

            /// <summary>
            /// The bit depth for the video stream of the files in this range.
            /// </summary>
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public int? VideoBitDepth { get; set; }

            /// <summary>
            /// The common name of the resolution for the files in this range (e.g., 720p, 1080p, etc.).
            /// </summary>
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public string? VideoResolution { get; set; }

            /// <summary>
            /// The viewport width for the video stream of the files in this range.
            /// </summary>
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public int? VideoWidth { get; set; }

            /// <summary>
            /// The viewport height for the video stream of the files in this range.
            /// </summary>
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public int? VideoHeight { get; set; }

            /// <summary>
            /// Indicates that the episodes in the group do have or don't have
            /// chapters.
            /// </summary>
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public bool? VideoHasChapters { get; set; }

            /// <summary>
            /// The audio codecs used in the files of this range (e.g., acc, ac3, dts, etc.).
            /// </summary>
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public string? AudioCodecs { get; set; }

            /// <summary>
            /// The ISO 639-1 two-letter language codes for the audio streams of the files in this range (e.g., "en" for English, "ja" for Japanese, etc.).
            /// </summary>
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public IEnumerable<string>? AudioLanguages { get; set; }

            /// <summary>
            /// The number of audio streams in the files in the range.
            /// </summary>
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public int? AudioStreamCount { get; set; }

            /// <summary>
            /// The subtitle/text codecs used in the files of this range (e.g., srt, ass, etc.).
            /// </summary>
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public string? SubtitleCodecs { get; set; }

            /// <summary>
            /// The ISO 639-1 two-letter language codes for the subtitle/text streams of the files in this range (e.g., "en" for English, "ja" for Japanese, etc.).
            /// </summary>
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public IEnumerable<string>? SubtitleLanguages { get; set; }

            /// <summary>
            /// The number of subtitle/text streams in the files in the range.
            /// </summary>
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public int? SubtitleStreamCount { get; set; }

            /// <summary>
            /// Dictionary of episode ranges and sizes by type (e.g., normal episode, special episode).
            /// </summary>
            public Dictionary<EpisodeType, EpisodeRangeByType> RangeByType { get; init; } = rangeByType;
            /// <summary>
            /// Sort by criteria.
            /// </summary>
            [JsonIgnore]
            internal SortByCriteria SortByCriteria { get; init; } = sortByCriteria;

            /// <summary>
            /// Episodes in the group.
            /// </summary>
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public IEnumerable<EpisodeDetails>? Episodes { get; set; }

            /// <summary>
            /// File locations in the group
            /// </summary>
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public IEnumerable<File.Location>? Locations { get; set; }
        }

        /// <summary>
        /// Represents the episode range and size by type.
        /// </summary>
        public class EpisodeRangeByType
        {
            /// <summary>
            /// Total number of episodes for the type in the range.
            /// </summary>
            public int Count { get; set; }

            /// <summary>
            /// The episode range included in this summary.
            /// </summary>
            /// <example>
            /// "01-03, 05, 07-134, 342-432"
            /// </example>
            public string Range { get; set; } = string.Empty;

            /// <summary>
            /// The accumulated file size in bytes across all files in this range.
            /// </summary>
            public long FileSize { get; set; }

            /// <summary>
            /// First episode in the range.
            /// </summary>
            [JsonIgnore]
            public int FirstEpisode { get; set; }

            /// <summary>
            /// Last episode in the range.
            /// </summary>
            [JsonIgnore]
            public int LastEpisode { get; set; }

            /// <summary>
            /// All episodes in the range, but as a sequence for programmatically comparison.
            /// </summary>
            /// <value></value>
            [JsonIgnore]
            public List<int> Sequence { get; set; } = [];
        }

        /// <summary>
        /// Determines how to sort the <see cref="EpisodeGroupSummary"/>s.
        /// </summary>
        public class SortByCriteria(IEnumerable<SortByCriteriaGroup> sortByCriteria) : IComparable<SortByCriteria>
        {
            public List<SortByCriteriaGroup> Groups { get; set; } = sortByCriteria.ToList();

            public int CompareTo(SortByCriteria? other)
            {
                if (other is null)
                    return -1;

                var totalLength = Math.Max(Groups.Count, other.Groups.Count);
                for (var index = 0; index < totalLength; index++)
                {
                    var criteriaA = index >= Groups.Count ? null : Groups[index];
                    var criteriaB = index >= other.Groups.Count ? null : other.Groups[index];
                    if (criteriaA is null && criteriaB is null)
                        return 0;
                    if (criteriaA is null)
                        return -1;
                    if (criteriaB is null)
                        return 1;

                    var result = criteriaA.CompareTo(criteriaB);
                    if (result != 0)
                        return result;
                }

                return 0;
            }
        }

        /// <summary>
        /// Represents the inner sort criteria group.
        /// </summary>
        public class SortByCriteriaGroup(EpisodeType type, int firstEpisode, int lastEpisode, List<int> sequence) : IComparable<SortByCriteriaGroup>
        {
            /// <summary>
            /// Episode type in the range.
            /// </summary>
            public EpisodeType Type { get; set; } = type;

            /// <summary>
            /// First episode in the range.
            /// </summary>
            public int FirstEpisode { get; set; } = firstEpisode;

            /// <summary>
            /// Last episode in the range.
            /// </summary>
            public int LastEpisode { get; set; } = lastEpisode;

            /// <summary>
            /// All episodes in the range, but as a sequence for programmatically comparison.
            /// </summary>
            public List<int> Sequence { get; set; } = sequence;

            public int CompareTo(SortByCriteriaGroup? other)
            {
                if (other is null)
                    return -1;

                var result = Type.CompareTo(other.Type);
                if (result != 0)
                    return result;

                result = FirstEpisode.CompareTo(other.FirstEpisode);
                if (result != 0)
                    return result;

                result = other.LastEpisode.CompareTo(LastEpisode);
                if (result != 0)
                    return result;

                result = other.Sequence.Count.CompareTo(Sequence.Count);
                if (result != 0)
                    return result;

                result = CompareSequences(Sequence, other.Sequence);
                if (result != 0)
                    return result;

                return 0;
            }
        }

        private class GroupByDetails : IEquatable<GroupByDetails>
        {
            public string? GroupName { get; set; }

            public string? GroupNameShort { get; set; }

            public int? FileVersion { get; set; }

            public FileSource? FileSource { get; set; }

            public int? VideoBitDepth { get; set; }

            public string? VideoResolution { get; set; }

            public int? VideoWidth { get; set; }

            public int? VideoHeight { get; set; }

            public string? VideoCodecs { get; set; }

            public string? AudioCodecs { get; set; }

            public string? AudioLanguages { get; set; }

            public int? AudioStreamCount { get; set; }

            public string? SubtitleCodecs { get; set; }

            public string? SubtitleLanguages { get; set; }

            public int? SubtitleStreamCount { get; set; }

            public string? FileLocation { get; set; }

            public bool? VideoHasChapters { get; set; }

            public bool? FileIsDeprecated { get; set; }

            public int? ImportFolder { get; set; }

            public string? ED2K { get; set; }

            public override bool Equals(object? obj)
            {
                return Equals(obj as GroupByDetails);
            }

            public bool Equals(GroupByDetails? other)
            {
                if (other == null)
                    return false;
                return
                    // GroupNameShort == other.GroupNameShort &&
                    GroupName == other.GroupName &&

                    FileVersion == other.FileVersion &&
                    FileSource == other.FileSource &&
                    FileLocation == other.FileLocation &&
                    FileIsDeprecated == other.FileIsDeprecated &&
                    ImportFolder == other.ImportFolder &&
                    ED2K == other.ED2K &&

                    VideoCodecs == other.VideoCodecs &&
                    VideoBitDepth == other.VideoBitDepth &&
                    VideoResolution == other.VideoResolution &&
                    // VideoWidth == other.VideoWidth &&
                    // VideoHeight == other.VideoHeight &&
                    VideoHasChapters == other.VideoHasChapters &&

                    AudioCodecs == other.AudioCodecs &&
                    AudioLanguages == other.AudioLanguages &&
                    AudioStreamCount == other.AudioStreamCount &&

                    SubtitleCodecs == other.SubtitleCodecs &&
                    SubtitleLanguages == other.SubtitleLanguages &&
                    SubtitleStreamCount == other.SubtitleStreamCount;
            }

            public override int GetHashCode()
                => HashCode.Combine(
                    HashCode.Combine(
                        // GroupNameShort,
                        GroupName
                    ),
                    HashCode.Combine(
                        FileVersion,
                        FileSource,
                        FileLocation,
                        FileIsDeprecated,
                        ImportFolder,
                        ED2K
                    ),
                    HashCode.Combine(
                        VideoCodecs,
                        VideoBitDepth,
                        VideoResolution,
                        // VideoWidth,
                        // VideoHeight,
                        VideoHasChapters
                    ),
                    HashCode.Combine(
                        AudioCodecs,
                        AudioLanguages,
                        AudioStreamCount
                    ),
                    HashCode.Combine(
                        SubtitleCodecs,
                        SubtitleLanguages,
                        SubtitleStreamCount
                    )
                );
        }

        public class EpisodeDetails
        {
            /// <summary>
            /// Shoko Episode ID.
            /// </summary>
            public required int EpisodeID { get; init; }

            /// <summary>
            /// Shoko File ID, if auto-linked.
            /// </summary>
            public required int FileID { get; init; }

            [JsonIgnore]
            public required SVR_VideoLocal_Place Location { get; init; }

            /// <summary>
            /// AniDB Episode Type.
            /// </summary>
            [JsonConverter(typeof(StringEnumConverter))]
            public required EpisodeType Type { get; init; }

            /// <summary>
            /// AniDB Episode Number.
            /// </summary>
            public required int Number { get; init; }

            /// <summary>
            /// File Size.
            /// </summary>
            public required long Size { get; init; }

            /// <summary>
            /// ED2K File Hash.
            /// </summary>
            public required string ED2K { get; init; }
        }

        public class FileSummaryOverview
        {
            /// <summary>
            /// The total size of all locally available files for a series
            /// </summary>
            public long TotalFileSize { get; set; }

            /// <summary>
            /// A summarized list of all the locally available release groups for a series.
            /// </summary>
            public List<string> ReleaseGroups { get; set; } = [];

            /// <summary>
            /// The list of all AniDB episode sources, and their associated file counts
            /// </summary>
            public List<SourcesByType> SourcesByType { get; set; } = [];
        }

        public class SourcesByType
        {
            /// <summary>
            /// The type of episode.
            /// </summary>
            [JsonConverter(typeof(StringEnumConverter))]
            public EpisodeType Type { get; set; }

            /// <summary>
            /// The source of the file for the episode
            /// </summary>
            public List<SourceGrouping> Sources { get; set; } = [];
        }

        public class SourceGrouping
        {
            /// <summary>
            /// The file source.
            /// </summary>
            [JsonConverter(typeof(StringEnumConverter))]
            public FileSource Type { get; set; }

            /// <summary>
            /// Amount of files with this file source.
            /// </summary>
            public int Count { get; set; }
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
            for (var i = 1; i < list.Count; i++)
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

        private static int CompareSequences(List<int> sequenceA, List<int> sequenceB)
        {
            for (var index = 0; index < sequenceA.Count; index++)
            {
                var result = sequenceA[index].CompareTo(sequenceB[index]);
                if (result != 0)
                    return result;
            }
            return 0;
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
            public HashSet<int> GroupIDs { get; set; } = [];

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
            public string URL { get; set; } = string.Empty;

            /// <summary>
            /// Gets or sets a flag indicating whether to enable preview mode for the theme.
            /// If true, the theme will be previewed without being added permanently.
            /// If false, the theme will be added as a permanent option in the Web UI.
            /// </summary>
            public bool Preview { get; set; } = false;
        }
    }
}
