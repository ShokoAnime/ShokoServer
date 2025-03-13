using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Shoko.Models;
using Shoko.Models.Enums;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Server.Extensions;
using Shoko.Server.MediaInfo;
using Shoko.Server.Models;
using Shoko.Server.Models.Release;

namespace Shoko.Server.Utilities;

public static class FileQualityFilter
{
    /*
    Types (This is to determine the order of these types to use)
        List

    Quality -- DatabaseReleaseInfo.LegacySource / DatabaseReleaseInfo.Source
        can be Array, but will prolly use List
    - BD
    - DVD
    - HDTV
    - TV
    - www
    - unknown

    Resolution (use rounding to determine where strange sizes fit)
        can be array, but will prolly use List
    - > 1080p
    - 1080p (BD)
    - 720p (BD Downscale and TV)
    - 540p (Nice DVD)
    - 480p (DVD)
    - < 480p (I really don't care at this low)

    Sub Groups (Need searching, will use fuzzy)
        List (Ordered set technically)
    ex.
    - Doki
    - ...
    - HorribleSubs

    Not configurable
    Higher version from the same release group, source, and resolution
    Chaptered over not chaptered

    make an enum
    reference said enum through a CompareByType

    */
    public static FileQualityPreferences Settings => Utils.SettingsProvider.GetSettings().FileQualityPreferences;

    #region Checks

    public static bool CheckFileKeep(VideoLocal video)
    {
        // Don't delete files with missing info. If it's not getting updated, then do it manually
        var anidbFile = video.ReleaseInfo;
        var allowUnknown = Utils.SettingsProvider.GetSettings().FileQualityPreferences.AllowDeletingFilesWithMissingInfo;
        if (IsNullOrUnknown(anidbFile) && !allowUnknown) return true;

        var result = true;
        var media = video.MediaInfo as IMediaInfo;
        foreach (var type in Settings.RequiredTypes)
        {
            result &= type switch
            {
                FileQualityFilterType.AUDIOCODEC =>
                    CheckAudioCodec(media),
                FileQualityFilterType.AUDIOSTREAMCOUNT =>
                    CheckAudioStreamCount(media),
                FileQualityFilterType.CHAPTER =>
                    CheckChaptered(anidbFile, media),
                FileQualityFilterType.RESOLUTION =>
                    CheckResolution(media),
                FileQualityFilterType.SOURCE =>
                    CheckSource(anidbFile),
                FileQualityFilterType.SUBGROUP =>
                    CheckSubGroup(anidbFile),
                FileQualityFilterType.SUBSTREAMCOUNT =>
                    CheckSubStreamCount(video),
                FileQualityFilterType.VERSION =>
                    CheckDeprecated(anidbFile),
                FileQualityFilterType.VIDEOCODEC =>
                    CheckVideoCodec(media),
                _ => true,
            };

            if (!result)
                break;
        }

        return result;
    }

    private static bool CheckAudioCodec(IMediaInfo media)
    {
        var codecs = media?.AudioStreams
            .Select(stream => stream.Codec.Simplified)
            .Where(codec => codec is not "unknown")
            .OrderBy(codec => codec)
            .ToList() ?? [];
        if (codecs.Count == 0)
            return false;

        var operationType = Settings.RequiredAudioCodecs.Operator;
        return operationType switch
        {
            FileQualityFilterOperationType.IN => codecs.FindInEnumerable(Settings.RequiredAudioCodecs.Value),
            FileQualityFilterOperationType.NOTIN => !codecs.FindInEnumerable(Settings.RequiredAudioCodecs.Value),
            _ => true
        };
    }

    private static bool CheckAudioStreamCount(IMediaInfo media)
    {
        var streamCount = media?.AudioStreams.Count ?? -1;
        if (streamCount == -1)
            return true;

        return Settings.RequiredAudioStreamCount.Operator switch
        {
            FileQualityFilterOperationType.EQUALS =>
                streamCount == Settings.RequiredAudioStreamCount.Value,
            FileQualityFilterOperationType.GREATER_EQ =>
                streamCount >= Settings.RequiredAudioStreamCount.Value,
            FileQualityFilterOperationType.LESS_EQ =>
                streamCount <= Settings.RequiredAudioStreamCount.Value,
            _ => true,
        };
    }

    private static bool CheckChaptered(StoredReleaseInfo anidbFile, IMediaInfo media)
    {
        return anidbFile?.IsChaptered ?? media?.Chapters.Any() ?? false;
    }

    private static bool CheckDeprecated(StoredReleaseInfo aniFile)
    {
        return !(aniFile?.IsCorrupted ?? false);
    }

    private static bool CheckResolution(IMediaInfo media)
    {
        if (media?.VideoStream is not { } videoStream || videoStream.Width == 0 || videoStream.Height == 0)
            return true;

        var resolution = MediaInfoUtility.GetStandardResolution(new(videoStream.Width, videoStream.Height));
        var resolutionArea = videoStream.Width * videoStream.Height;
        return Settings.RequiredResolutions.Operator switch
        {
            FileQualityFilterOperationType.EQUALS =>
                resolution.Equals(Settings.RequiredResolutions.Value.FirstOrDefault()),
            FileQualityFilterOperationType.GREATER_EQ =>
                MediaInfoUtility.ResolutionArea169
                    .Concat(MediaInfoUtility.ResolutionArea43)
                    .Where(pair => resolutionArea >= pair.Key)
                    .Select(pair => pair.Value)
                    .FindInEnumerable(Settings.RequiredResolutions.Value),
            FileQualityFilterOperationType.LESS_EQ =>
                MediaInfoUtility.ResolutionArea169
                    .Concat(MediaInfoUtility.ResolutionArea43)
                    .Where(pair => resolutionArea <= pair.Key)
                    .Select(pair => pair.Value)
                    .FindInEnumerable(Settings.RequiredResolutions.Value),
            FileQualityFilterOperationType.IN =>
                Settings.RequiredResolutions.Value.Contains(resolution),
            FileQualityFilterOperationType.NOTIN =>
                !Settings.RequiredResolutions.Value.Contains(resolution),
            _ => false,
        };
    }

    private static bool CheckSource(StoredReleaseInfo aniFile)
    {
        if (IsNullOrUnknown(aniFile))
        {
            return false;
        }

        var operationType = Settings.RequiredSources.Operator;
        var source = aniFile.LegacySource.ToLowerInvariant();
        if (FileQualityPreferences.SimplifiedSources.TryGetValue(source, out var simplifiedSource))
        {
            source = simplifiedSource;
        }

        return operationType switch
        {
            FileQualityFilterOperationType.IN => Settings.RequiredSources.Value.Contains(source),
            FileQualityFilterOperationType.NOTIN => !Settings.RequiredSources.Value.Contains(source),
            _ => true
        };
    }

    private static bool CheckSubGroup(StoredReleaseInfo aniFile)
    {
        if (IsNullOrUnknown(aniFile))
        {
            return false;
        }

        var operationType = Settings.RequiredSubGroups.Operator;
        return operationType switch
        {
            FileQualityFilterOperationType.IN => Settings.RequiredSubGroups.Value.Contains(aniFile.GroupName.ToLowerInvariant()) ||
                                                 Settings.RequiredSubGroups.Value.Contains(aniFile.GroupShortName.ToLowerInvariant()),
            FileQualityFilterOperationType.NOTIN => !Settings.RequiredSubGroups.Value.Contains(aniFile.GroupName.ToLowerInvariant()) &&
                                                    !Settings.RequiredSubGroups.Value.Contains(aniFile.GroupShortName.ToLowerInvariant()),
            _ => true
        };
    }

    private static bool CheckSubStreamCount(VideoLocal file)
    {
        var streamCount = file?.MediaInfo?.TextStreams.Count ?? -1;
        if (streamCount == -1)
        {
            return true;
        }

        var operationType = Settings.RequiredSubStreamCount.Operator;
        return operationType switch
        {
            FileQualityFilterOperationType.EQUALS => streamCount == Settings.RequiredSubStreamCount.Value,
            FileQualityFilterOperationType.GREATER_EQ => streamCount >= Settings.RequiredSubStreamCount.Value,
            FileQualityFilterOperationType.LESS_EQ => streamCount <= Settings.RequiredSubStreamCount.Value,
            _ => true
        };
    }

    private static bool CheckVideoCodec(IMediaInfo media)
    {
        var codecs = media?.TextStreams
            .Select(stream => stream.Codec.Simplified)
            .Where(codec => codec is not "unknown")
            .OrderBy(codec => codec)
            .ToList() ?? [];
        if (codecs.Count == 0)
            return false;

        return Settings.RequiredVideoCodecs.Operator switch
        {
            FileQualityFilterOperationType.IN =>
                Settings.RequiredVideoCodecs.Value.FindInEnumerable(codecs),
            FileQualityFilterOperationType.NOTIN =>
                !Settings.RequiredVideoCodecs.Value.FindInEnumerable(codecs),
            _ => true,
        };
    }

    #endregion

    #region Comparisons

    // -1 if oldFile is to be deleted, 0 if they are comparatively equal, 1 if the oldFile is better
    public static int CompareTo(VideoLocal newVideo, VideoLocal oldVideo)
    {
        if (newVideo == null && oldVideo == null)
            return 0;
        if (newVideo == null)
            return 1;
        if (oldVideo == null)
            return -1;

        var newMedia = newVideo.MediaInfo;
        var newAnidbFile = newVideo.ReleaseInfo;
        var oldMedia = oldVideo.MediaInfo;
        var oldAnidbFile = oldVideo.ReleaseInfo;
        foreach (var type in Settings.PreferredTypes)
        {
            var result = (type) switch
            {
                FileQualityFilterType.AUDIOCODEC =>
                    CompareAudioCodecTo(newMedia, oldMedia),
                FileQualityFilterType.AUDIOSTREAMCOUNT =>
                    CompareAudioStreamCountTo(newMedia, oldMedia),
                FileQualityFilterType.CHAPTER =>
                    CompareChapterTo(newMedia, newAnidbFile, oldMedia, oldAnidbFile),
                FileQualityFilterType.RESOLUTION =>
                    CompareResolutionTo(newMedia, oldMedia),
                FileQualityFilterType.SOURCE =>
                    CompareSourceTo(newAnidbFile, oldAnidbFile),
                FileQualityFilterType.SUBGROUP =>
                    CompareSubGroupTo(newAnidbFile, oldAnidbFile),
                FileQualityFilterType.SUBSTREAMCOUNT =>
                    CompareSubStreamCountTo(newMedia, oldMedia),
                FileQualityFilterType.VERSION =>
                    CompareVersionTo(newAnidbFile, oldAnidbFile, newMedia, oldMedia),
                FileQualityFilterType.VIDEOCODEC =>
                    CompareVideoCodecTo(newMedia, oldMedia),
                _ => 0,
            };

            if (result != 0)
                return result;
        }

        return 0;
    }

    private static int CompareAudioCodecTo(IMediaInfo newMedia, IMediaInfo oldMedia)
    {
        var newCodecs = newMedia?.AudioStreams
            .Select(stream => stream.Codec.Simplified)
            .Where(codec => codec is not "unknown")
            .OrderBy(codec => codec)
            .ToList() ?? [];
        var oldCodecs = oldMedia?.AudioStreams
            .Select(stream => stream.Codec.Simplified)
            .Where(codec => codec is not "unknown")
            .OrderBy(codec => codec)
            .ToList() ?? [];
        // compare side by side, average codec quality would be vague and annoying, defer to number of audio tracks
        if (newCodecs.Count != oldCodecs.Count)
            return 0;

        var max = Math.Min(newCodecs.Count, oldCodecs.Count);
        for (var i = 0; i < max; i++)
        {
            var newCodec = newCodecs[i];
            var oldCodec = oldCodecs[i];
            var newIndex = Settings.PreferredAudioCodecs.IndexOf(newCodec);
            var oldIndex = Settings.PreferredAudioCodecs.IndexOf(oldCodec);
            if (newIndex == -1 || oldIndex == -1)
                continue;

            var result = newIndex.CompareTo(oldIndex);
            if (result != 0)
                return result;
        }

        return 0;
    }

    private static int CompareAudioStreamCountTo(IMediaInfo newMedia, IMediaInfo oldMedia)
    {
        var newStreamCount = newMedia?.AudioStreams.Count ?? 0;
        var oldStreamCount = oldMedia?.AudioStreams.Count ?? 0;
        return oldStreamCount.CompareTo(newStreamCount);
    }

    private static int CompareChapterTo(IMediaInfo newMedia, StoredReleaseInfo newFile, IMediaInfo oldMedia, StoredReleaseInfo oldFile)
    {
        var newIsChaptered = newFile?.IsChaptered ?? newMedia?.Chapters.Any() ?? false;
        var oldIsChaptered = oldFile?.IsChaptered ?? oldMedia?.Chapters.Any() ?? false;
        return oldIsChaptered.CompareTo(newIsChaptered);
    }

    private static int CompareResolutionTo(IMediaInfo newMedia, IMediaInfo oldMedia)
    {
        var newRes = newMedia?.VideoStream is { } newVideo ? newVideo.Resolution : "unknown";
        var oldRes = oldMedia?.VideoStream is { } oldVideo ? oldVideo.Resolution : "unknown";
        if (newRes == "unknown" && oldRes == "unknown")
            return 0;
        if (newRes == "unknown")
            return 1;
        if (oldRes == "unknown")
            return -1;

        var newIndex = Settings.PreferredResolutions.IndexOf(newRes);
        var oldIndex = Settings.PreferredResolutions.IndexOf(oldRes);
        if (newIndex == -1 && oldIndex == -1)
            return 0;
        if (newIndex == -1)
            return 1;
        if (oldIndex == -1)
            return -1;

        return newIndex.CompareTo(oldIndex);
    }

    private static int CompareSourceTo(StoredReleaseInfo newFile, StoredReleaseInfo oldFile)
    {
        var newAnidbFileIsNullOrUnknown = IsNullOrUnknown(newFile);
        var oldAnidbFileIsNullOrUnknown = IsNullOrUnknown(oldFile);
        if (newAnidbFileIsNullOrUnknown && oldAnidbFileIsNullOrUnknown)
            return 0;
        if (newAnidbFileIsNullOrUnknown)
            return 1;
        if (oldAnidbFileIsNullOrUnknown)
            return -1;

        var newSource = newFile!.LegacySource.ToLowerInvariant();
        if (FileQualityPreferences.SimplifiedSources.TryGetValue(newSource, out var value))
            newSource = value;

        var oldSource = oldFile!.LegacySource.ToLowerInvariant();
        if (FileQualityPreferences.SimplifiedSources.TryGetValue(oldSource, out value))
            oldSource = value;

        var newIndex = Settings.PreferredSources.IndexOf(newSource);
        var oldIndex = Settings.PreferredSources.IndexOf(oldSource);
        if (newIndex == -1 && oldIndex == -1)
            return 0;
        if (newIndex == -1)
            return 1;
        if (oldIndex == -1)
            return -1;
        return newIndex.CompareTo(oldIndex);
    }

    private static int CompareSubGroupTo(StoredReleaseInfo newFile, StoredReleaseInfo oldFile)
    {
        var newAnidbFileIsNullOrUnknown = IsNullOrUnknown(newFile);
        var oldAnidbFileIsNullOrUnknown = IsNullOrUnknown(oldFile);
        if (newAnidbFileIsNullOrUnknown && oldAnidbFileIsNullOrUnknown)
            return 0;
        if (newAnidbFileIsNullOrUnknown)
            return 1;
        if (oldAnidbFileIsNullOrUnknown)
            return -1;

        var newIndex = -1;
        if (!string.IsNullOrEmpty(newFile.GroupName))
            newIndex = Settings.PreferredSubGroups.IndexOf(newFile.GroupName);
        if (newIndex == -1 && !string.IsNullOrEmpty(newFile.GroupShortName))
            newIndex = Settings.PreferredSubGroups.IndexOf(newFile.GroupShortName);

        var oldIndex = -1;
        if (!string.IsNullOrEmpty(oldFile.GroupName))
            oldIndex = Settings.PreferredSubGroups.IndexOf(oldFile.GroupName);
        if (oldIndex == -1 && !string.IsNullOrEmpty(oldFile.GroupShortName))
            oldIndex = Settings.PreferredSubGroups.IndexOf(oldFile.GroupShortName);

        if (newIndex == -1 && oldIndex == -1)
            return 0;
        if (newIndex == -1)
            return 1;
        if (oldIndex == -1)
            return -1;
        return newIndex.CompareTo(oldIndex);
    }

    private static int CompareSubStreamCountTo(IMediaInfo newMedia, IMediaInfo oldMedia)
    {
        var newStreamCount = newMedia?.TextStreams.Count ?? 0;
        var oldStreamCount = oldMedia?.TextStreams.Count ?? 0;
        return oldStreamCount.CompareTo(newStreamCount);
    }

    private static int CompareVersionTo(StoredReleaseInfo newFile, StoredReleaseInfo oldFile, IMediaInfo newMedia, IMediaInfo oldMedia)
    {
        var newAnidbFileIsNullOrUnknown = IsNullOrUnknown(newFile);
        var oldAnidbFileIsNullOrUnknown = IsNullOrUnknown(oldFile);
        if (newAnidbFileIsNullOrUnknown && oldAnidbFileIsNullOrUnknown)
            return 0;
        if (newAnidbFileIsNullOrUnknown)
            return 1;
        if (oldAnidbFileIsNullOrUnknown)
            return -1;

        if (newFile!.GroupID != oldFile!.GroupID)
            return 0;

        var newBitDepth = newMedia?.VideoStream?.BitDepth ?? -1;
        var oldBitDepth = oldMedia?.VideoStream?.BitDepth ?? -1;
        if (newBitDepth != oldBitDepth)
            return 0;

        var newSimpleCodec = newMedia?.VideoStream?.Codec.Simplified;
        var oldSimpleCodec = oldMedia?.VideoStream?.Codec.Simplified;
        if (!string.Equals(newSimpleCodec, oldSimpleCodec))
            return 0;

        return oldFile.Revision.CompareTo(newFile.Revision);
    }

    private static int CompareVideoCodecTo(IMediaInfo newMedia, IMediaInfo oldMedia)
    {
        var newCodecs = newMedia?.VideoStreams
            .Select(stream => stream.Codec.Simplified)
            .Where(codec => codec is not "unknown")
            .OrderBy(codec => codec)
            .ToList() ?? [];
        var oldCodecs = oldMedia?.VideoStreams
            .Select(stream => stream.Codec.Simplified)
            .Where(codec => codec is not "unknown")
            .OrderBy(codec => codec)
            .ToList() ?? [];
        // compare side by side, average codec quality would be vague and annoying, defer to number of audio tracks
        if (newCodecs.Count != oldCodecs.Count)
            return 0;

        var max = Math.Min(newCodecs.Count, oldCodecs.Count);
        for (var i = 0; i < max; i++)
        {
            var newCodec = newCodecs[i];
            var oldCodec = oldCodecs[i];
            var newIndex = Settings.PreferredVideoCodecs.IndexOf(newCodec);
            var oldIndex = Settings.PreferredVideoCodecs.IndexOf(oldCodec);
            if (newIndex == -1 || oldIndex == -1)
            {
                continue;
            }

            var result = newIndex.CompareTo(oldIndex);
            if (result != 0)
                return result;

            var newBitDepth = newMedia?.VideoStream?.BitDepth ?? -1;
            var oldBitDepth = oldMedia?.VideoStream?.BitDepth ?? -1;
            if (newBitDepth == -1 || oldBitDepth == -1)
                continue;

            if (newBitDepth == 8 && oldBitDepth == 10)
                return Settings.Prefer8BitVideo ? -1 : 1;

            if (newBitDepth == 10 && oldBitDepth == 8)
                return Settings.Prefer8BitVideo ? 1 : -1;
        }

        return 0;
    }

    #endregion

    #region Information from Models (Operations that aren't simple)

    private static bool IsNullOrUnknown([NotNullWhen(false)][MaybeNullWhen(true)] StoredReleaseInfo file)
    {
        // Check file.
        if (file is null ||
            string.IsNullOrWhiteSpace(file.LegacySource) ||
            string.Equals(file.LegacySource, "unknown", StringComparison.InvariantCultureIgnoreCase) ||
            string.Equals(file.LegacySource, "raw", StringComparison.InvariantCultureIgnoreCase))
            return true;

        // Check release group.
        if (string.IsNullOrWhiteSpace(file.GroupName) ||
            string.Equals(file.GroupName, "unknown", StringComparison.InvariantCultureIgnoreCase) ||
            string.Equals(file.GroupName, "raw", StringComparison.InvariantCultureIgnoreCase))
            return true;

        if (string.IsNullOrWhiteSpace(file.GroupShortName) ||
            string.Equals(file.GroupShortName, "unknown", StringComparison.InvariantCultureIgnoreCase) ||
            string.Equals(file.GroupShortName, "raw", StringComparison.InvariantCultureIgnoreCase))
            return true;

        return false;
    }

    #endregion
}
