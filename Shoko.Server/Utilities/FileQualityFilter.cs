using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Shoko.Models;
using Shoko.Models.Enums;
using Shoko.Models.MediaInfo;
using Shoko.Models.Server;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Server.Extensions;
using Shoko.Server.Models;

namespace Shoko.Server.Utilities;

public static class FileQualityFilter
{
    /*
    Types (This is to determine the order of these types to use)
        List

    Quality -- AniDB_File.File_Source
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

    public static bool CheckFileKeep(SVR_VideoLocal video)
    {
        // Don't delete files with missing info. If it's not getting updated, then do it manually
        var anidbFile = video.AniDBFile;
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

    private static bool CheckChaptered(AniDB_File anidbFile, IMediaInfo media)
    {
        return anidbFile?.IsChaptered ?? media?.Chapters.Any() ?? false;
    }

    private static bool CheckDeprecated(AniDB_File aniFile)
    {
        return !(aniFile?.IsDeprecated ?? false);
    }

    private static bool CheckResolution(IMediaInfo media)
    {
        if (media?.VideoStream is not { } videoStream || videoStream.Width == 0 || videoStream.Height == 0)
            return true;

        var resolution = MediaInfoUtils.GetStandardResolution(new(videoStream.Width, videoStream.Height));
        var resolutionArea = videoStream.Width * videoStream.Height;
        return Settings.RequiredResolutions.Operator switch
        {
            FileQualityFilterOperationType.EQUALS =>
                resolution.Equals(Settings.RequiredResolutions.Value.FirstOrDefault()),
            FileQualityFilterOperationType.GREATER_EQ =>
                MediaInfoUtils.ResolutionArea169
                    .Concat(MediaInfoUtils.ResolutionArea43)
                    .Where(pair => resolutionArea >= pair.Key)
                    .Select(pair => pair.Value)
                    .FindInEnumerable(Settings.RequiredResolutions.Value),
            FileQualityFilterOperationType.LESS_EQ =>
                MediaInfoUtils.ResolutionArea169
                    .Concat(MediaInfoUtils.ResolutionArea43)
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

    private static bool CheckSource(SVR_AniDB_File aniFile)
    {
        if (IsNullOrUnknown(aniFile))
        {
            return false;
        }

        var operationType = Settings.RequiredSources.Operator;
        var source = aniFile.File_Source.ToLowerInvariant();
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

    private static bool CheckSubGroup(SVR_AniDB_File aniFile)
    {
        if (IsNullOrUnknown(aniFile))
        {
            return false;
        }

        var operationType = Settings.RequiredSubGroups.Operator;
        return operationType switch
        {
            FileQualityFilterOperationType.IN => Settings.RequiredSubGroups.Value.Contains(aniFile.Anime_GroupName.ToLowerInvariant()) ||
                                                 Settings.RequiredSubGroups.Value.Contains(aniFile.Anime_GroupNameShort.ToLowerInvariant()),
            FileQualityFilterOperationType.NOTIN => !Settings.RequiredSubGroups.Value.Contains(aniFile.Anime_GroupName.ToLowerInvariant()) &&
                                                    !Settings.RequiredSubGroups.Value.Contains(aniFile.Anime_GroupNameShort.ToLowerInvariant()),
            _ => true
        };
    }

    private static bool CheckSubStreamCount(SVR_VideoLocal file)
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
    public static int CompareTo(SVR_VideoLocal newVideo, SVR_VideoLocal oldVideo)
    {
        if (newVideo == null && oldVideo == null)
            return 0;
        if (newVideo == null)
            return 1;
        if (oldVideo == null)
            return -1;

        var newMedia = newVideo.MediaInfo;
        var newAnidbFile = newVideo.AniDBFile;
        var oldMedia = oldVideo.MediaInfo;
        var oldAnidbFile = oldVideo.AniDBFile;
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

    private static int CompareChapterTo(IMediaInfo newMedia, SVR_AniDB_File newFile, IMediaInfo oldMedia, SVR_AniDB_File oldFile)
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

    private static int CompareSourceTo(SVR_AniDB_File newFile, SVR_AniDB_File oldFile)
    {
        var newAnidbFileIsNullOrUnknown = IsNullOrUnknown(newFile);
        var oldAnidbFileIsNullOrUnknown = IsNullOrUnknown(oldFile);
        if (newAnidbFileIsNullOrUnknown && oldAnidbFileIsNullOrUnknown)
            return 0;
        if (newAnidbFileIsNullOrUnknown)
            return 1;
        if (oldAnidbFileIsNullOrUnknown)
            return -1;

        var newSource = newFile!.File_Source.ToLowerInvariant();
        if (FileQualityPreferences.SimplifiedSources.TryGetValue(newSource, out var value))
            newSource = value;

        var oldSource = oldFile!.File_Source.ToLowerInvariant();
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

    private static int CompareSubGroupTo(SVR_AniDB_File newFile, SVR_AniDB_File oldFile)
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
        var newGroup = newFile!.ReleaseGroup;
        if (!string.IsNullOrEmpty(newGroup.GroupName))
            newIndex = Settings.PreferredSubGroups.IndexOf(newGroup.GroupName);
        if (newIndex == -1 && !string.IsNullOrEmpty(newGroup.GroupNameShort))
            newIndex = Settings.PreferredSubGroups.IndexOf(newGroup.GroupNameShort);

        var oldIndex = -1;
        var oldGroup = oldFile!.ReleaseGroup;
        if (!string.IsNullOrEmpty(oldGroup.GroupName))
            oldIndex = Settings.PreferredSubGroups.IndexOf(oldGroup.GroupName);
        if (oldIndex == -1 && !string.IsNullOrEmpty(oldGroup.GroupNameShort))
            oldIndex = Settings.PreferredSubGroups.IndexOf(oldGroup.GroupNameShort);

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

    private static int CompareVersionTo(SVR_AniDB_File newFile, SVR_AniDB_File oldFile, IMediaInfo newMedia, IMediaInfo oldMedia)
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

        return oldFile.FileVersion.CompareTo(newFile.FileVersion);
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

    private static bool IsNullOrUnknown([NotNullWhen(false)][MaybeNullWhen(true)] SVR_AniDB_File file)
    {
        // Check file.
        if (file is null ||
            string.IsNullOrWhiteSpace(file.File_Source) ||
            string.Equals(file.File_Source, "unknown", StringComparison.InvariantCultureIgnoreCase) ||
            string.Equals(file.File_Source, "raw", StringComparison.InvariantCultureIgnoreCase))
            return true;

        // Check release group.
        var releaseGroup = file.ReleaseGroup;
        if (string.IsNullOrWhiteSpace(releaseGroup.GroupName) ||
            string.Equals(releaseGroup.GroupName, "unknown", StringComparison.InvariantCultureIgnoreCase) ||
            string.Equals(releaseGroup.GroupName, "raw", StringComparison.InvariantCultureIgnoreCase))
            return true;

        if (string.IsNullOrWhiteSpace(releaseGroup.GroupNameShort) ||
            string.Equals(releaseGroup.GroupNameShort, "unknown", StringComparison.InvariantCultureIgnoreCase) ||
            string.Equals(releaseGroup.GroupNameShort, "raw", StringComparison.InvariantCultureIgnoreCase))
            return true;

        return false;
    }

    #endregion
}
