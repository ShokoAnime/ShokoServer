using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Video.Enums;
using Shoko.Abstractions.Video.Release;
using Shoko.Server.MediaInfo;
using Shoko.Server.Models.Release;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Services;
using Xunit;

namespace Shoko.Tests.Services;

/// <summary>
/// Documents how <see cref="VideoReleaseGroupingService"/> groups
/// <see cref="VideoLocal_Place"/> objects into <see cref="VideoReleaseCandidate"/>
/// buckets. These tests describe the intent rather than lock in behaviour —
/// think of them as executable spec comments.
/// </summary>
public class VideoReleaseGroupingTests
{
    // ── helpers ──────────────────────────────────────────────────────────────

    private static readonly VideoReleaseGroupingService _grouper = new(null!, null!);

    private static IReadOnlyList<VideoReleaseCandidate> Group(IEnumerable<ResolvedVideoPlace> places)
        => _grouper.Group(places);

    private static VideoLocal_Place MakePlace(int placeId, int videoId, int folderId, string relativePath)
        => new() { ID = placeId, VideoID = videoId, ManagedFolderID = folderId, RelativePath = relativePath };

    private static VideoLocal MakeVideo(int id, string hash, long size, MediaContainer? media = null)
        => new() { VideoLocalID = id, Hash = hash, FileSize = size, MediaInfo = media };

    /// <summary>
    /// Creates a <see cref="StoredReleaseInfo"/> for test use.
    /// <para>
    /// <paramref name="episodes"/> accepts the standard Shoko episode identifier
    /// format: a bare number for normal episodes (e.g. <c>"1"</c>, <c>"12"</c>),
    /// or a letter-prefixed number for other types (<c>"S1"</c> = special 1,
    /// <c>"C2"</c> = credits 2, <c>"T1"</c> = trailer 1, <c>"P1"</c> = parody 1,
    /// <c>"O1"</c> = other 1).
    /// </para>
    /// </summary>
    private static StoredReleaseInfo MakeSri(
        string hash, long size,
        string groupId, string groupSource, string groupName, string groupShortName,
        ReleaseSource source,
        string audioLangs = "ja", string subLangs = "en",
        int version = 1,
        string[]? episodes = null,
        bool isCorrupted = false,
        bool? isChaptered = null)
    {
        var sri = new StoredReleaseInfo
        {
            ED2K = hash,
            FileSize = size,
            GroupID = groupId,
            GroupSource = groupSource,
            GroupName = groupName,
            GroupShortName = groupShortName,
            Source = source,
            EmbeddedAudioLanguages = audioLangs,
            EmbeddedSubtitleLanguages = subLangs,
            Version = version,
            IsCorrupted = isCorrupted,
            IsChaptered = isChaptered,
        };
        if (episodes is { Length: > 0 })
            sri.CrossReferences = episodes
                .Select(ParseEpisode)
                .ToList<IReleaseVideoCrossReference>();
        return sri;
    }

    /// <summary>
    /// Parses a Shoko episode identifier string (e.g. <c>"1"</c>, <c>"S2"</c>,
    /// <c>"C1"</c>) into an <see cref="EmbeddedCrossReference"/>.
    /// </summary>
    private static EmbeddedCrossReference ParseEpisode(string s)
    {
        if (string.IsNullOrEmpty(s))
            throw new ArgumentException("Episode string cannot be empty.", nameof(s));
        var (type, rest) = char.IsLetter(s[0])
            ? (char.ToUpperInvariant(s[0]) switch
            {
                'S' => EpisodeType.Special,
                'C' => EpisodeType.Credits,
                'T' => EpisodeType.Trailer,
                'P' => EpisodeType.Parody,
                'O' => EpisodeType.Other,
                _ => EpisodeType.Episode,
            }, s[1..])
            : (EpisodeType.Episode, s);
        return new EmbeddedCrossReference
        {
            EpisodeType = type,
            EpisodeNumber = int.Parse(rest),
            PercentageStart = 0,
            PercentageEnd = 100,
        };
    }

    /// <summary>
    /// Builds a <see cref="MediaContainer"/> with the most common fields set.
    /// The GeneralStream must always be present and have Duration &gt; 0 so
    /// that <see cref="MediaContainer.IsUsable"/> returns true.
    /// </summary>
    private static MediaContainer MakeMedia(
        string videoFormat = "AVC", string videoCodecId = "V_MPEG4/ISO/AVC",
        int width = 1920, int height = 1080, int bitDepth = 10,
        string audioFormat = "FLAC", string audioCodecId = "A_FLAC", string audioLang = "ja",
        string container = "Matroska")
        => new()
        {
            media = new Media
            {
                track =
                [
                    new GeneralStream { Format = container, Duration = 1400 },
                    new VideoStream
                    {
                        Format = videoFormat, CodecID = videoCodecId,
                        Width = width, Height = height, BitDepth = bitDepth,
                    },
                    new AudioStream { Format = audioFormat, CodecID = audioCodecId, Language = audioLang },
                ]
            }
        };

    // ── first-pass key grouping ───────────────────────────────────────────────

    /// <summary>
    /// Files from the same group, same source, same languages, same directory
    /// all collapse into a single candidate — the happy path for a typical
    /// fansub release such as SubsPlease WEB.
    /// </summary>
    [Fact]
    public void SameGroupSameSourceSameFolder_ProducesSingleCandidate()
    {
        var media = MakeMedia();
        var resolved = new[]
        {
            new ResolvedVideoPlace(
                MakePlace(1, 1, 8, "Log Horizon/[SubsPlease] Log Horizon - 01 [1080p].mkv"),
                MakeVideo(1, "AAA", 500_000_000, media),
                MakeSri("AAA", 500_000_000, "1337", "AniDB", "SubsPlease", "SubsPlease", ReleaseSource.Web, episodes: ["1"])),
            new ResolvedVideoPlace(
                MakePlace(2, 2, 8, "Log Horizon/[SubsPlease] Log Horizon - 02 [1080p].mkv"),
                MakeVideo(2, "BBB", 510_000_000, media),
                MakeSri("BBB", 510_000_000, "1337", "AniDB", "SubsPlease", "SubsPlease", ReleaseSource.Web, episodes: ["2"])),
            new ResolvedVideoPlace(
                MakePlace(3, 3, 8, "Log Horizon/[SubsPlease] Log Horizon - 03 [1080p].mkv"),
                MakeVideo(3, "CCC", 505_000_000, media),
                MakeSri("CCC", 505_000_000, "1337", "AniDB", "SubsPlease", "SubsPlease", ReleaseSource.Web, episodes: ["3"])),
        };

        var candidates = Group(resolved);

        Assert.Single(candidates);
        Assert.Equal(3, candidates[0].Places.Count);
        Assert.Equal("SubsPlease", candidates[0].GroupShortName);
        Assert.Equal(ReleaseSource.Web, candidates[0].Source);
        Assert.True(candidates[0].HasReleaseInfo);
    }

    /// <summary>
    /// The same group releasing BD and DVD episodes of the same show produces
    /// two candidates — source is part of the first-pass key.
    /// This matches the real Air library where eps 1-12 are BD but ep 13 is DVD.
    /// </summary>
    [Fact]
    public void SameGroupDifferentSource_ProducesTwoCandidates()
    {
        var bdMedia = MakeMedia(width: 1280, height: 720);
        var dvdMedia = MakeMedia(width: 720, height: 480, audioFormat: "AC3", audioCodecId: "A_AC3");

        var resolved = new[]
        {
            new ResolvedVideoPlace(
                MakePlace(1, 1, 1, "Air/[Doki] Air - 01 (1280x720 Hi10P BD FLAC) [77D45BED].mkv"),
                MakeVideo(1, "AAA", 600_000_000, bdMedia),
                MakeSri("AAA", 600_000_000, "584", "AniDB", "Doki", "Doki", ReleaseSource.BluRay, episodes: ["1"])),
            new ResolvedVideoPlace(
                MakePlace(2, 2, 1, "Air/[Doki] Air - 13 (720x480 h264 DVD AC3) [19AFC854].mkv"),
                MakeVideo(2, "BBB", 400_000_000, dvdMedia),
                MakeSri("BBB", 400_000_000, "584", "AniDB", "Doki", "Doki", ReleaseSource.DVD, episodes: ["13"])),
        };

        var candidates = Group(resolved);

        Assert.Equal(2, candidates.Count);
        Assert.Contains(candidates, c => c.Source == ReleaseSource.BluRay);
        Assert.Contains(candidates, c => c.Source == ReleaseSource.DVD);
    }

    /// <summary>
    /// Files from the same group in different directories produce separate
    /// candidates. The parent directory is the primary scope of a release.
    /// </summary>
    [Fact]
    public void SameGroupDifferentFolders_ProducesSeparateCandidates()
    {
        var media = MakeMedia();
        var resolved = new[]
        {
            new ResolvedVideoPlace(
                MakePlace(1, 1, 8, "One Piece/[SubsPlease] One Piece - 01 [1080p].mkv"),
                MakeVideo(1, "AAA", 700_000_000, media),
                MakeSri("AAA", 700_000_000, "1337", "AniDB", "SubsPlease", "SubsPlease", ReleaseSource.Web, episodes: ["1"])),
            new ResolvedVideoPlace(
                MakePlace(2, 2, 8, "Naruto/[SubsPlease] Naruto - 01 [1080p].mkv"),
                MakeVideo(2, "BBB", 700_000_000, media),
                MakeSri("BBB", 700_000_000, "1337", "AniDB", "SubsPlease", "SubsPlease", ReleaseSource.Web, episodes: ["1"])),
        };

        var candidates = Group(resolved);

        Assert.Equal(2, candidates.Count);
        Assert.Contains(candidates.SelectMany(c => c.Places), p => p.RelativePath.StartsWith("One Piece"));
        Assert.Contains(candidates.SelectMany(c => c.Places), p => p.RelativePath.StartsWith("Naruto"));
    }

    /// <summary>
    /// Different release groups in the same folder produce separate candidates.
    /// </summary>
    [Fact]
    public void DifferentGroupsSameFolder_ProducesSeparateCandidates()
    {
        var media = MakeMedia();
        var resolved = new[]
        {
            new ResolvedVideoPlace(
                MakePlace(1, 1, 1, "Clannad/[Doki] Clannad - 01 (BD 1080p FLAC) [ABC].mkv"),
                MakeVideo(1, "AAA", 800_000_000, media),
                MakeSri("AAA", 800_000_000, "584", "AniDB", "Doki", "Doki", ReleaseSource.BluRay, episodes: ["1"])),
            new ResolvedVideoPlace(
                MakePlace(2, 2, 1, "Clannad/[Coalgirls] Clannad - 01 (1920x1080 Hi10P BD FLAC) [DEF].mkv"),
                MakeVideo(2, "BBB", 900_000_000, media),
                MakeSri("BBB", 900_000_000, "412", "AniDB", "Coalgirls", "Coalgirls", ReleaseSource.BluRay, episodes: ["1"])),
        };

        var candidates = Group(resolved);

        Assert.Equal(2, candidates.Count);
        Assert.Contains(candidates, c => c.GroupShortName == "Doki");
        Assert.Contains(candidates, c => c.GroupShortName == "Coalgirls");
    }

    /// <summary>
    /// A dual-audio release is distinct from a Japanese-only release of the
    /// same group and source. Language sets are part of the first-pass key.
    /// </summary>
    [Fact]
    public void DualAudioVsMonoAudio_ProduceSeparateCandidates()
    {
        var media = MakeMedia();
        var resolved = new[]
        {
            new ResolvedVideoPlace(
                MakePlace(1, 1, 8, "Cowboy Bebop/[GHOST] Cowboy Bebop - 01 [BD].mkv"),
                MakeVideo(1, "AAA", 900_000_000, media),
                MakeSri("AAA", 900_000_000, "777", "AniDB", "GHOST", "GHOST",
                    ReleaseSource.BluRay, audioLangs: "en,ja", episodes: ["1"])),
            new ResolvedVideoPlace(
                MakePlace(2, 2, 8, "Cowboy Bebop/[AB-RMX] Cowboy Bebop - 01 [BD].mkv"),
                MakeVideo(2, "BBB", 850_000_000, media),
                MakeSri("BBB", 850_000_000, "888", "AniDB", "AB-RMX", "AB-RMX",
                    ReleaseSource.BluRay, audioLangs: "ja", episodes: ["1"])),
        };

        var candidates = Group(resolved);

        Assert.Equal(2, candidates.Count);
    }

    /// <summary>
    /// A 1080p and a 720p release from the same group are separate candidates.
    /// Resolution is bucketed and included in the key.
    /// </summary>
    [Fact]
    public void DifferentResolutions_ProduceSeparateCandidates()
    {
        var hd1080 = MakeMedia(width: 1920, height: 1080);
        var hd720 = MakeMedia(width: 1280, height: 720);

        var resolved = new[]
        {
            new ResolvedVideoPlace(
                MakePlace(1, 1, 8, "SAO/[HorribleSubs] SAO - 01 [1080p].mkv"),
                MakeVideo(1, "AAA", 900_000_000, hd1080),
                MakeSri("AAA", 900_000_000, "432", "AniDB", "HorribleSubs", "HorribleSubs", ReleaseSource.Web, episodes: ["1"])),
            new ResolvedVideoPlace(
                MakePlace(2, 2, 8, "SAO/[HorribleSubs] SAO - 01 [720p].mkv"),
                MakeVideo(2, "BBB", 500_000_000, hd720),
                MakeSri("BBB", 500_000_000, "432", "AniDB", "HorribleSubs", "HorribleSubs", ReleaseSource.Web, episodes: ["1"])),
        };

        var candidates = Group(resolved);

        Assert.Equal(2, candidates.Count);
        Assert.Contains(candidates, c => c.Resolution == "1080p");
        Assert.Contains(candidates, c => c.Resolution == "720p");
    }

    /// <summary>
    /// H264 and HEVC encodes of the same show from the same group are separate
    /// candidates. Video codec is part of the first-pass key.
    /// </summary>
    [Fact]
    public void DifferentVideoCodecs_ProduceSeparateCandidates()
    {
        var h264Media = MakeMedia(videoFormat: "AVC", videoCodecId: "V_MPEG4/ISO/AVC");
        var hevcMedia = MakeMedia(videoFormat: "HEVC", videoCodecId: "V_MPEGH/ISO/HEVC");

        var resolved = new[]
        {
            new ResolvedVideoPlace(
                MakePlace(1, 1, 8, "Show/[Group] Show - 01 [h264].mkv"),
                MakeVideo(1, "AAA", 800_000_000, h264Media),
                MakeSri("AAA", 800_000_000, "100", "AniDB", "Group", "Group", ReleaseSource.Web, episodes: ["1"])),
            new ResolvedVideoPlace(
                MakePlace(2, 2, 8, "Show/[Group] Show - 01 [hevc].mkv"),
                MakeVideo(2, "BBB", 400_000_000, hevcMedia),
                MakeSri("BBB", 400_000_000, "100", "AniDB", "Group", "Group", ReleaseSource.Web, episodes: ["1"])),
        };

        var candidates = Group(resolved);

        Assert.Equal(2, candidates.Count);
        Assert.Contains(candidates, c => c.VideoCodec == "H264");
        Assert.Contains(candidates, c => c.VideoCodec == "HEVC");
    }

    /// <summary>
    /// Files in different import folders are always separate candidates, even
    /// when every other signal matches.
    /// </summary>
    [Fact]
    public void SameGroupDifferentImportFolders_ProduceSeparateCandidates()
    {
        var media = MakeMedia();
        var resolved = new[]
        {
            new ResolvedVideoPlace(
                MakePlace(1, 1, folderId: 1, "Toradora/[Chihiro] Toradora - 01 [BD].mkv"),
                MakeVideo(1, "AAA", 700_000_000, media),
                MakeSri("AAA", 700_000_000, "209", "AniDB", "Chihiro", "Chihiro", ReleaseSource.BluRay, episodes: ["1"])),
            new ResolvedVideoPlace(
                MakePlace(2, 2, folderId: 8, "Toradora/[Chihiro] Toradora - 01 [BD].mkv"),
                MakeVideo(2, "AAA", 700_000_000, media),
                MakeSri("AAA", 700_000_000, "209", "AniDB", "Chihiro", "Chihiro", ReleaseSource.BluRay, episodes: ["1"])),
        };

        var candidates = Group(resolved);

        Assert.Equal(2, candidates.Count);
    }

    // ── missing-field tolerance (fuzzy grouping) ─────────────────────────────

    /// <summary>
    /// A file whose SRI has no audio language tag (e.g. AniDB never recorded
    /// language for that specific hash) should still join the group that has
    /// the language set, not form a separate candidate.
    /// Missing language = wildcard, not a distinct value.
    /// </summary>
    [Fact]
    public void MissingAudioLanguageOnOneFile_MergesWithMatchingGroup()
    {
        var media = MakeMedia();
        var resolved = new[]
        {
            new ResolvedVideoPlace(
                MakePlace(1, 1, 1, "Steins Gate/[FFF] Steins Gate - 01 (BD FLAC) [AAA].mkv"),
                MakeVideo(1, "AAA", 700_000_000, media),
                MakeSri("AAA", 700_000_000, "102", "AniDB", "FFF", "FFF", ReleaseSource.BluRay,
                    audioLangs: "ja", episodes: ["1"])),
            new ResolvedVideoPlace(
                MakePlace(2, 2, 1, "Steins Gate/[FFF] Steins Gate - 02 (BD FLAC) [BBB].mkv"),
                MakeVideo(2, "BBB", 710_000_000, media),
                // ep 2's SRI has no audio language recorded
                MakeSri("BBB", 710_000_000, "102", "AniDB", "FFF", "FFF", ReleaseSource.BluRay,
                    audioLangs: "", episodes: ["2"])),
            new ResolvedVideoPlace(
                MakePlace(3, 3, 1, "Steins Gate/[FFF] Steins Gate - 03 (BD FLAC) [CCC].mkv"),
                MakeVideo(3, "CCC", 705_000_000, media),
                MakeSri("CCC", 705_000_000, "102", "AniDB", "FFF", "FFF", ReleaseSource.BluRay,
                    audioLangs: "ja", episodes: ["3"])),
        };

        var candidates = Group(resolved);

        Assert.Single(candidates);
        Assert.Equal(3, candidates[0].Places.Count);
    }

    /// <summary>
    /// A file with no SRI but named and placed identically to the rest of the
    /// group should join that group rather than forming its own candidate.
    /// The file shares the same codec/resolution/folder signals.
    /// </summary>
    [Fact]
    public void UnrecognizedFileSameSignals_MergesWithSriBackedGroup()
    {
        var media = MakeMedia(width: 1280, height: 720);
        var resolved = new[]
        {
            new ResolvedVideoPlace(
                MakePlace(1, 1, 1, "Air/[Doki] Air - 01 (1280x720 Hi10P BD FLAC).mkv"),
                MakeVideo(1, "AAA", 600_000_000, media),
                MakeSri("AAA", 600_000_000, "584", "AniDB", "Doki", "Doki", ReleaseSource.BluRay, episodes: ["1"])),
            new ResolvedVideoPlace(
                MakePlace(2, 2, 1, "Air/[Doki] Air - 02 (1280x720 Hi10P BD FLAC).mkv"),
                MakeVideo(2, "BBB", 610_000_000, media),
                MakeSri("BBB", 610_000_000, "584", "AniDB", "Doki", "Doki", ReleaseSource.BluRay, episodes: ["2"])),
            // ep 3 was never submitted to AniDB — no SRI — but codec+resolution match
            new ResolvedVideoPlace(
                MakePlace(3, 3, 1, "Air/[Doki] Air - 03 (1280x720 Hi10P BD FLAC).mkv"),
                MakeVideo(3, "CCC", 605_000_000, media), null),
        };

        var candidates = Group(resolved);

        Assert.Single(candidates);
        Assert.Equal(3, candidates[0].Places.Count);
        // HasReleaseInfo is false because one file has no SRI
        Assert.False(candidates[0].HasReleaseInfo);
        // Group metadata still populated from the SRI-backed files
        Assert.Equal("Doki", candidates[0].GroupShortName);
    }

    /// <summary>
    /// An unrecognized file whose codec differs from the SRI-backed group still
    /// forms its own candidate, even though it has no group info itself.
    /// A known codec conflict is a hard separator.
    /// </summary>
    [Fact]
    public void UnrecognizedFileConflictingCodec_StaysSeparate()
    {
        var h264Media = MakeMedia(videoFormat: "AVC", videoCodecId: "V_MPEG4/ISO/AVC");
        var hevcMedia = MakeMedia(videoFormat: "HEVC", videoCodecId: "V_MPEGH/ISO/HEVC");

        var resolved = new[]
        {
            new ResolvedVideoPlace(
                MakePlace(1, 1, 1, "Show/[Group] Show - 01 (h264).mkv"),
                MakeVideo(1, "AAA", 700_000_000, h264Media),
                MakeSri("AAA", 700_000_000, "100", "AniDB", "Group", "Group", ReleaseSource.Web, episodes: ["1"])),
            new ResolvedVideoPlace(
                MakePlace(2, 2, 1, "Show/[Group] Show - 02 (h264).mkv"),
                MakeVideo(2, "BBB", 710_000_000, h264Media),
                MakeSri("BBB", 710_000_000, "100", "AniDB", "Group", "Group", ReleaseSource.Web, episodes: ["2"])),
            // No SRI, but codec is HEVC — conflicts with the H264 group
            new ResolvedVideoPlace(
                MakePlace(3, 3, 1, "Show/[Group] Show - 03 (hevc).mkv"),
                MakeVideo(3, "CCC", 400_000_000, hevcMedia), null),
        };

        var candidates = Group(resolved);

        Assert.Equal(2, candidates.Count);
        Assert.Contains(candidates, c => c.VideoCodec == "H264" && c.Places.Count == 2);
        Assert.Contains(candidates, c => c.VideoCodec == "HEVC" && c.Places.Count == 1);
    }

    /// <summary>
    /// When one file has audio language [ja, en] and another has [ja], these
    /// are different, complete language sets — not a missing-data case. They
    /// must produce separate candidates.
    /// </summary>
    [Fact]
    public void DifferentCompleteLangSets_ProduceSeparateCandidates()
    {
        var media = MakeMedia();
        var resolved = new[]
        {
            new ResolvedVideoPlace(
                MakePlace(1, 1, 8, "Show/[Group] Show - 01 [dual].mkv"),
                MakeVideo(1, "AAA", 900_000_000, media),
                MakeSri("AAA", 900_000_000, "100", "AniDB", "Group", "Group",
                    ReleaseSource.Web, audioLangs: "ja,en", episodes: ["1"])),
            new ResolvedVideoPlace(
                MakePlace(2, 2, 8, "Show/[Group] Show - 01 [mono].mkv"),
                MakeVideo(2, "BBB", 600_000_000, media),
                MakeSri("BBB", 600_000_000, "100", "AniDB", "Group", "Group",
                    ReleaseSource.Web, audioLangs: "ja", episodes: ["1"])),
        };

        var candidates = Group(resolved);

        // Both sides have complete lang data that conflicts → two candidates
        Assert.Equal(2, candidates.Count);
    }

    // ── episode-collision split ───────────────────────────────────────────────

    /// <summary>
    /// The standard mixed-version release: most episodes are v1, a handful
    /// were corrected to v2. No two files cover the same episode, so the
    /// group stays together as one candidate.
    /// This is the Air/[Doki] pattern observed in the real library.
    /// </summary>
    [Fact]
    public void MixedVersionSingleRelease_StaysAsOneCandidate()
    {
        var media = MakeMedia(width: 1280, height: 720);
        var doki = (string hash, long size, string ep, int ver) =>
            MakeSri(hash, size, "584", "AniDB", "Doki", "Doki", ReleaseSource.BluRay, version: ver, episodes: [ep]);

        var resolved = new[]
        {
            // ep 1, 2 — v1, never patched
            new ResolvedVideoPlace(MakePlace(1, 1, 1, "Air/[Doki] Air - 01 v1.mkv"),
                MakeVideo(1, "A1", 600_000_000, media), doki("A1", 600_000_000, "1", 1)),
            new ResolvedVideoPlace(MakePlace(2, 2, 1, "Air/[Doki] Air - 02 v1.mkv"),
                MakeVideo(2, "A2", 610_000_000, media), doki("A2", 610_000_000, "2", 1)),
            // ep 3 patched → v2, but ep 3 only appears once (v2 replaced v1)
            new ResolvedVideoPlace(MakePlace(3, 3, 1, "Air/[Doki] Air - 03v2.mkv"),
                MakeVideo(3, "A3v2", 612_000_000, media), doki("A3v2", 612_000_000, "3", 2)),
            // ep 7 also patched
            new ResolvedVideoPlace(MakePlace(4, 4, 1, "Air/[Doki] Air - 07v2.mkv"),
                MakeVideo(4, "A7v2", 608_000_000, media), doki("A7v2", 608_000_000, "7", 2)),
        };

        var candidates = Group(resolved);

        // All four files cover different episodes — no collision → one release
        Assert.Single(candidates);
        Assert.Equal(4, candidates[0].Places.Count);
    }

    /// <summary>
    /// When a group releases a full v2 batch (every episode re-encoded) alongside
    /// the original v1, and the user keeps both complete sets, the grouper splits
    /// them into two candidates — one per version.
    /// The trigger is that every episode appears in both a v1 and a v2 file.
    /// </summary>
    [Fact]
    public void CompleteV1AndV2Batches_SplitIntoSeparateCandidates()
    {
        var media = MakeMedia(width: 1280, height: 720);
        var doki = (string hash, long size, string ep, int ver) =>
            MakeSri(hash, size, "584", "AniDB", "Doki", "Doki", ReleaseSource.BluRay, version: ver, episodes: [ep]);

        var resolved = new[]
        {
            // v1 batch (original release)
            new ResolvedVideoPlace(MakePlace(1, 1, 1, "Air/[Doki] Air - 01v1.mkv"),
                MakeVideo(1, "A1v1", 600_000_000, media), doki("A1v1", 600_000_000, "1", 1)),
            new ResolvedVideoPlace(MakePlace(2, 2, 1, "Air/[Doki] Air - 02v1.mkv"),
                MakeVideo(2, "A2v1", 610_000_000, media), doki("A2v1", 610_000_000, "2", 1)),
            new ResolvedVideoPlace(MakePlace(3, 3, 1, "Air/[Doki] Air - 03v1.mkv"),
                MakeVideo(3, "A3v1", 605_000_000, media), doki("A3v1", 605_000_000, "3", 1)),
            // v2 batch — every episode has a counterpart, so all collide
            new ResolvedVideoPlace(MakePlace(4, 4, 1, "Air/[Doki] Air - 01v2.mkv"),
                MakeVideo(4, "A1v2", 620_000_000, media), doki("A1v2", 620_000_000, "1", 2)),
            new ResolvedVideoPlace(MakePlace(5, 5, 1, "Air/[Doki] Air - 02v2.mkv"),
                MakeVideo(5, "A2v2", 630_000_000, media), doki("A2v2", 630_000_000, "2", 2)),
            new ResolvedVideoPlace(MakePlace(6, 6, 1, "Air/[Doki] Air - 03v2.mkv"),
                MakeVideo(6, "A3v2", 625_000_000, media), doki("A3v2", 625_000_000, "3", 2)),
        };

        var candidates = Group(resolved);

        Assert.Equal(2, candidates.Count);
        Assert.All(candidates, c => Assert.Equal(3, c.Places.Count));
    }

    /// <summary>
    /// A release can include a special alongside the normal episodes. Here
    /// episode 1 and special 1 are in the same release — there is no collision
    /// because they cover different episode types.
    /// </summary>
    [Fact]
    public void SpecialAlongsideNormalEpisodes_ProducesSingleCandidate()
    {
        var media = MakeMedia(width: 1280, height: 720);
        var doki = (string hash, long size, string ep) =>
            MakeSri(hash, size, "584", "AniDB", "Doki", "Doki", ReleaseSource.BluRay, episodes: [ep]);

        var resolved = new[]
        {
            new ResolvedVideoPlace(MakePlace(1, 1, 1, "Air/[Doki] Air - 01 (BD FLAC).mkv"),
                MakeVideo(1, "A1", 600_000_000, media), doki("A1", 600_000_000, "1")),
            new ResolvedVideoPlace(MakePlace(2, 2, 1, "Air/[Doki] Air - 02 (BD FLAC).mkv"),
                MakeVideo(2, "A2", 610_000_000, media), doki("A2", 610_000_000, "2")),
            // S1 is a special — (Special, 1) ≠ (Episode, 1), no collision
            new ResolvedVideoPlace(MakePlace(3, 3, 1, "Air/[Doki] Air - S01 (BD FLAC).mkv"),
                MakeVideo(3, "AS1", 400_000_000, media), doki("AS1", 400_000_000, "S1")),
        };

        var candidates = Group(resolved);

        Assert.Single(candidates);
        Assert.Equal(3, candidates[0].Places.Count);
    }

    /// <summary>
    /// When every episode in a subset (here 5, 10, 15) has both a v1 and a v2 file
    /// and there are no other episodes, the collision is total — every episode in
    /// the bucket has duplicates — so the bucket splits into one candidate per
    /// version. This is the "full collision on specific episodes" case.
    /// </summary>
    [Fact]
    public void AllEpisodesHaveBothVersions_SplitIntoTwoCandidates()
    {
        var media = MakeMedia(width: 1280, height: 720);
        var doki = (string hash, long size, string ep, int ver) =>
            MakeSri(hash, size, "584", "AniDB", "Doki", "Doki", ReleaseSource.BluRay, version: ver, episodes: [ep]);

        var resolved = new[]
        {
            // v1 files for eps 5, 10, 15
            new ResolvedVideoPlace(MakePlace(1, 1, 1, "Show/[Doki] Show - 05v1.mkv"),
                MakeVideo(1, "h5v1", 600_000_000, media), doki("h5v1", 600_000_000, "5", 1)),
            new ResolvedVideoPlace(MakePlace(2, 2, 1, "Show/[Doki] Show - 10v1.mkv"),
                MakeVideo(2, "h10v1", 610_000_000, media), doki("h10v1", 610_000_000, "10", 1)),
            new ResolvedVideoPlace(MakePlace(3, 3, 1, "Show/[Doki] Show - 15v1.mkv"),
                MakeVideo(3, "h15v1", 605_000_000, media), doki("h15v1", 605_000_000, "15", 1)),
            // v2 files for the same eps — every episode now has two files
            new ResolvedVideoPlace(MakePlace(4, 4, 1, "Show/[Doki] Show - 05v2.mkv"),
                MakeVideo(4, "h5v2", 620_000_000, media), doki("h5v2", 620_000_000, "5", 2)),
            new ResolvedVideoPlace(MakePlace(5, 5, 1, "Show/[Doki] Show - 10v2.mkv"),
                MakeVideo(5, "h10v2", 625_000_000, media), doki("h10v2", 625_000_000, "10", 2)),
            new ResolvedVideoPlace(MakePlace(6, 6, 1, "Show/[Doki] Show - 15v2.mkv"),
                MakeVideo(6, "h15v2", 615_000_000, media), doki("h15v2", 615_000_000, "15", 2)),
        };

        var candidates = Group(resolved);

        // Every episode (5, 10, 15) is covered by both a v1 and a v2 file →
        // full collision → split into two candidates, one per version.
        Assert.Equal(2, candidates.Count);
        Assert.All(candidates, c => Assert.Equal(3, c.Places.Count));
        Assert.Contains(candidates, c => c.Version == 1);
        Assert.Contains(candidates, c => c.Version == 2);
    }

    /// <summary>
    /// When a v2 batch exists for most episodes but not all (one episode was
    /// never re-encoded), the collision is only partial. The grouper conservatively
    /// keeps everything in one candidate rather than splitting.
    /// </summary>
    [Fact]
    public void PartialV2Batch_StaysAsOneCandidateConservatively()
    {
        var media = MakeMedia(width: 1280, height: 720);
        var doki = (string hash, long size, string ep, int ver) =>
            MakeSri(hash, size, "584", "AniDB", "Doki", "Doki", ReleaseSource.BluRay, version: ver, episodes: [ep]);

        var resolved = new[]
        {
            // v1 originals for all three episodes
            new ResolvedVideoPlace(MakePlace(1, 1, 1, "Air/[Doki] Air - 01v1.mkv"),
                MakeVideo(1, "A1v1", 600_000_000, media), doki("A1v1", 600_000_000, "1", 1)),
            new ResolvedVideoPlace(MakePlace(2, 2, 1, "Air/[Doki] Air - 02v1.mkv"),
                MakeVideo(2, "A2v1", 610_000_000, media), doki("A2v1", 610_000_000, "2", 1)),
            new ResolvedVideoPlace(MakePlace(3, 3, 1, "Air/[Doki] Air - 03v1.mkv"),
                MakeVideo(3, "A3v1", 605_000_000, media), doki("A3v1", 605_000_000, "3", 1)),
            // v2 only for ep 1 and 2 — ep 3 was never re-encoded
            new ResolvedVideoPlace(MakePlace(4, 4, 1, "Air/[Doki] Air - 01v2.mkv"),
                MakeVideo(4, "A1v2", 620_000_000, media), doki("A1v2", 620_000_000, "1", 2)),
            new ResolvedVideoPlace(MakePlace(5, 5, 1, "Air/[Doki] Air - 02v2.mkv"),
                MakeVideo(5, "A2v2", 630_000_000, media), doki("A2v2", 630_000_000, "2", 2)),
        };

        var candidates = Group(resolved);

        // ep 3 only has v1 → not every episode collides → keep together
        Assert.Single(candidates);
        Assert.Equal(5, candidates[0].Places.Count);
    }

    /// <summary>
    /// When a corrupt file is the only file covering its episode it must be
    /// kept — there is no better alternative.  Mixed quality within a release
    /// (ep 1 clean, ep 2 corrupt, ep 3 clean) is normal and should not split.
    /// </summary>
    [Fact]
    public void MixedQualityNoCoverage_CorruptFileIncludedAsOnlyOption()
    {
        var media = MakeMedia(width: 1280, height: 720);
        var doki = (string hash, long size, string ep, bool corrupted) =>
            MakeSri(hash, size, "584", "AniDB", "Doki", "Doki", ReleaseSource.BluRay,
                version: 1, episodes: [ep], isCorrupted: corrupted);

        var resolved = new[]
        {
            new ResolvedVideoPlace(MakePlace(1, 1, 1, "Air/[Doki] Air - 01.mkv"),
                MakeVideo(1, "A1", 600_000_000, media), doki("A1", 600_000_000, "1", false)),
            // ep 2's only file is marked corrupt — no other option
            new ResolvedVideoPlace(MakePlace(2, 2, 1, "Air/[Doki] Air - 02 [corrupt].mkv"),
                MakeVideo(2, "A2x", 610_000_000, media), doki("A2x", 610_000_000, "2", true)),
            new ResolvedVideoPlace(MakePlace(3, 3, 1, "Air/[Doki] Air - 03.mkv"),
                MakeVideo(3, "A3", 605_000_000, media), doki("A3", 605_000_000, "3", false)),
        };

        var candidates = Group(resolved);

        // No episode collision → keep all together; the corrupt ep 2 has no alternative
        Assert.Single(candidates);
        Assert.Equal(3, candidates[0].Places.Count);
        // Aggregate: any file corrupt → candidate marks itself corrupt
        Assert.True(candidates[0].IsCorrupted);
    }

    /// <summary>
    /// When every episode is covered by two files of the same version but one
    /// copy is corrupt and the other clean, the grouper splits them into two
    /// quality-tier candidates so the caller can prefer the non-corrupt set.
    /// </summary>
    [Fact]
    public void EpisodeCollisionSameVersionQualityDiffers_SplitsByQualityTier()
    {
        var media = MakeMedia(width: 1280, height: 720);
        var doki = (string hash, long size, string ep, bool corrupted) =>
            MakeSri(hash, size, "584", "AniDB", "Doki", "Doki", ReleaseSource.BluRay,
                version: 1, episodes: [ep], isCorrupted: corrupted);

        var resolved = new[]
        {
            // Clean set
            new ResolvedVideoPlace(MakePlace(1, 1, 1, "Air/[Doki] Air - 01.mkv"),
                MakeVideo(1, "A1c", 600_000_000, media), doki("A1c", 600_000_000, "1", false)),
            new ResolvedVideoPlace(MakePlace(2, 2, 1, "Air/[Doki] Air - 02.mkv"),
                MakeVideo(2, "A2c", 610_000_000, media), doki("A2c", 610_000_000, "2", false)),
            new ResolvedVideoPlace(MakePlace(3, 3, 1, "Air/[Doki] Air - 03.mkv"),
                MakeVideo(3, "A3c", 605_000_000, media), doki("A3c", 605_000_000, "3", false)),
            // Corrupt set — every episode has a counterpart, same version
            new ResolvedVideoPlace(MakePlace(4, 4, 1, "Air/[Doki] Air - 01 [bad].mkv"),
                MakeVideo(4, "A1x", 600_000_000, media), doki("A1x", 600_000_000, "1", true)),
            new ResolvedVideoPlace(MakePlace(5, 5, 1, "Air/[Doki] Air - 02 [bad].mkv"),
                MakeVideo(5, "A2x", 610_000_000, media), doki("A2x", 610_000_000, "2", true)),
            new ResolvedVideoPlace(MakePlace(6, 6, 1, "Air/[Doki] Air - 03 [bad].mkv"),
                MakeVideo(6, "A3x", 605_000_000, media), doki("A3x", 605_000_000, "3", true)),
        };

        var candidates = Group(resolved);

        // Same version but different quality tiers → split so the better set can be preferred
        Assert.Equal(2, candidates.Count);
        var clean = candidates.Single(c => !c.IsCorrupted);
        var corrupt = candidates.Single(c => c.IsCorrupted);
        Assert.Equal(3, clean.Places.Count);
        Assert.Equal(3, corrupt.Places.Count);
    }

    // ── unrecognized files (no StoredReleaseInfo) ────────────────────────────

    /// <summary>
    /// Unrecognized files in the same directory with identical MediaInfo signals
    /// are grouped together. HasReleaseInfo is false for these candidates.
    /// </summary>
    [Fact]
    public void UnrecognizedFiles_SameFolderSameMediaInfo_GroupedTogether()
    {
        var media = MakeMedia();
        var resolved = new[]
        {
            new ResolvedVideoPlace(
                MakePlace(1, 1, 8, "Unknown Show/episode01.mkv"),
                MakeVideo(1, "AAA", 500_000_000, media), null),
            new ResolvedVideoPlace(
                MakePlace(2, 2, 8, "Unknown Show/episode02.mkv"),
                MakeVideo(2, "BBB", 510_000_000, media), null),
            new ResolvedVideoPlace(
                MakePlace(3, 3, 8, "Unknown Show/episode03.mkv"),
                MakeVideo(3, "CCC", 505_000_000, media), null),
        };

        var candidates = Group(resolved);

        Assert.Single(candidates);
        Assert.Equal(3, candidates[0].Places.Count);
        Assert.False(candidates[0].HasReleaseInfo);
        Assert.Null(candidates[0].GroupShortName);
    }

    /// <summary>
    /// Unrecognized files with different codecs in the same folder produce
    /// separate candidates. MediaInfo is the only signal without release info.
    /// </summary>
    [Fact]
    public void UnrecognizedFiles_DifferentCodecsSameFolder_ProduceSeparateCandidates()
    {
        var h264Media = MakeMedia(videoFormat: "AVC", videoCodecId: "V_MPEG4/ISO/AVC");
        var hevcMedia = MakeMedia(videoFormat: "HEVC", videoCodecId: "V_MPEGH/ISO/HEVC");

        var resolved = new[]
        {
            new ResolvedVideoPlace(
                MakePlace(1, 1, 8, "Mixed/[OldGroup] Show - 01 [h264].mkv"),
                MakeVideo(1, "AAA", 500_000_000, h264Media), null),
            new ResolvedVideoPlace(
                MakePlace(2, 2, 8, "Mixed/[NewGroup] Show - 01 [hevc].mkv"),
                MakeVideo(2, "BBB", 300_000_000, hevcMedia), null),
        };

        var candidates = Group(resolved);

        Assert.Equal(2, candidates.Count);
        Assert.Contains(candidates, c => c.VideoCodec == "H264");
        Assert.Contains(candidates, c => c.VideoCodec == "HEVC");
    }

    /// <summary>
    /// When some files in a folder lack a StoredReleaseInfo, the unrecognized
    /// files merge into the SRI-backed group (their missing group field is a
    /// wildcard) as long as the codec and resolution agree.
    /// The resulting single candidate has HasReleaseInfo=false because not
    /// every file has an SRI record.
    /// </summary>
    [Fact]
    public void MixedReleaseInfoCoverage_UnrecognizedFileMergesIntoGroup()
    {
        var media = MakeMedia();
        var resolved = new[]
        {
            new ResolvedVideoPlace(
                MakePlace(1, 1, 8, "Show/[FFF] Show - 01 (BD 1080p FLAC) [AAA].mkv"),
                MakeVideo(1, "AAA", 600_000_000, media),
                MakeSri("AAA", 600_000_000, "102", "AniDB", "FFF", "FFF", ReleaseSource.BluRay, episodes: ["1"])),
            new ResolvedVideoPlace(
                MakePlace(2, 2, 8, "Show/[FFF] Show - 02 (BD 1080p FLAC) [BBB].mkv"),
                MakeVideo(2, "BBB", 610_000_000, media),
                MakeSri("BBB", 610_000_000, "102", "AniDB", "FFF", "FFF", ReleaseSource.BluRay, episodes: ["2"])),
            // ep 3 was never submitted to AniDB — no SRI — but codec/resolution match the group
            new ResolvedVideoPlace(
                MakePlace(3, 3, 8, "Show/[FFF] Show - 03 (BD 1080p FLAC) [CCC].mkv"),
                MakeVideo(3, "CCC", 605_000_000, media), null),
        };

        var candidates = Group(resolved);

        // Fuzzy grouping: missing group = wildcard → merges into the FFF bucket
        Assert.Single(candidates);
        Assert.Equal(3, candidates[0].Places.Count);
        Assert.False(candidates[0].HasReleaseInfo); // not every file has an SRI
        Assert.Equal("FFF", candidates[0].GroupShortName); // metadata from the SRI-backed representative
    }

    // ── key uniqueness ────────────────────────────────────────────────────────

    /// <summary>
    /// The key is a SHA-256 hash of quality signals, not a location identifier.
    /// Two candidates with different quality profiles produce different keys;
    /// two candidates with identical quality profiles but in different folders
    /// would produce the same key (they represent the same release type).
    /// </summary>
    [Fact]
    public void DifferentQualityProfiles_ProduceDifferentKeys()
    {
        var media1080 = MakeMedia(width: 1920, height: 1080);
        var media720 = MakeMedia(width: 1280, height: 720);
        var resolved = new[]
        {
            // Doki, BD, 1080p
            new ResolvedVideoPlace(
                MakePlace(1, 1, 8, "A/[Doki] A - 01.mkv"),
                MakeVideo(1, "AAA", 500_000_000, media1080),
                MakeSri("AAA", 500_000_000, "584", "AniDB", "Doki", "Doki", ReleaseSource.BluRay, episodes: ["1"])),
            new ResolvedVideoPlace(
                MakePlace(2, 2, 8, "A/[Doki] A - 02.mkv"),
                MakeVideo(2, "BBB", 510_000_000, media1080),
                MakeSri("BBB", 510_000_000, "584", "AniDB", "Doki", "Doki", ReleaseSource.BluRay, episodes: ["2"])),
            // SubsPlease, Web, 720p — different group, source, resolution
            new ResolvedVideoPlace(
                MakePlace(3, 3, 8, "B/[SubsPlease] B - 01.mkv"),
                MakeVideo(3, "CCC", 700_000_000, media720),
                MakeSri("CCC", 700_000_000, "1337", "AniDB", "SubsPlease", "SubsPlease", ReleaseSource.Web, episodes: ["1"])),
        };

        var candidates = Group(resolved);

        Assert.Equal(2, candidates.Count);
        Assert.NotEqual(candidates[0].Key, candidates[1].Key);
    }

    /// <summary>
    /// Two candidates with identical quality profiles but in different parent
    /// directories produce the same key — the key is a quality fingerprint,
    /// not a location identifier.
    /// </summary>
    [Fact]
    public void IdenticalQualityProfiles_ProduceSameKey()
    {
        var media = MakeMedia();
        var resolved = new[]
        {
            new ResolvedVideoPlace(
                MakePlace(1, 1, 8, "One Piece/[SubsPlease] One Piece - 01 [1080p].mkv"),
                MakeVideo(1, "AAA", 700_000_000, media),
                MakeSri("AAA", 700_000_000, "1337", "AniDB", "SubsPlease", "SubsPlease", ReleaseSource.Web, episodes: ["1"])),
            new ResolvedVideoPlace(
                MakePlace(2, 2, 8, "Naruto/[SubsPlease] Naruto - 01 [1080p].mkv"),
                MakeVideo(2, "BBB", 700_000_000, media),
                MakeSri("BBB", 700_000_000, "1337", "AniDB", "SubsPlease", "SubsPlease", ReleaseSource.Web, episodes: ["1"])),
        };

        var candidates = Group(resolved);

        // Still two candidates (different directories), but same quality key
        Assert.Equal(2, candidates.Count);
        Assert.Equal(candidates[0].Key, candidates[1].Key);
    }
}
