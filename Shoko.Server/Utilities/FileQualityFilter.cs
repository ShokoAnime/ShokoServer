using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Shoko.Models;
using Shoko.Models.Enums;
using Shoko.Models.MediaInfo;
using Shoko.Models.Server;
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

    public static bool CheckFileKeep(SVR_VideoLocal file)
    {
        var result = true;
        var allowUnknown = Utils.SettingsProvider.GetSettings().FileQualityPreferences.AllowDeletingFilesWithMissingInfo;

        var aniFile = file?.AniDBFile;
        // Don't delete files with missing info. If it's not getting updated, then do it manually
        if (IsNullOrUnknown(aniFile) && !allowUnknown) return true;

        foreach (var type in Settings.RequiredTypes)
        {
            if (!result)
            {
                break;
            }

            switch (type)
            {
                case FileQualityFilterType.AUDIOCODEC:
                    result &= CheckAudioCodec(file);
                    break;
                case FileQualityFilterType.AUDIOSTREAMCOUNT:
                    result &= CheckAudioStreamCount(file);
                    break;
                case FileQualityFilterType.CHAPTER:
                    if (aniFile == null)
                    {
                        return false;
                    }

                    result &= CheckChaptered(file);
                    break;
                case FileQualityFilterType.RESOLUTION:
                    result &= CheckResolution(file);
                    break;
                case FileQualityFilterType.SOURCE:
                    if (aniFile == null)
                    {
                        return false;
                    }

                    result &= CheckSource(aniFile);
                    break;
                case FileQualityFilterType.SUBGROUP:
                    if (aniFile == null)
                    {
                        return false;
                    }

                    result &= CheckSubGroup(aniFile);
                    break;
                case FileQualityFilterType.SUBSTREAMCOUNT:
                    result &= CheckSubStreamCount(file);
                    break;
                case FileQualityFilterType.VERSION:
                    if (aniFile == null)
                    {
                        return false;
                    }

                    result &= CheckDeprecated(aniFile);
                    break;
                case FileQualityFilterType.VIDEOCODEC:
                    if (aniFile == null)
                    {
                        return false;
                    }

                    result &= CheckVideoCodec(file);
                    break;
            }
        }

        return result;
    }

    private static bool CheckAudioCodec(SVR_VideoLocal aniFile)
    {
        var codecs =
            aniFile?.MediaInfo?.AudioStreams.Select(LegacyMediaUtils.TranslateCodec).OrderBy(a => a)
                .ToArray() ?? Array.Empty<string>();
        if (codecs.Length == 0)
        {
            return false;
        }

        var operationType = Settings.RequiredAudioCodecs.Operator;
        return operationType switch
        {
            FileQualityFilterOperationType.IN => codecs.FindInEnumerable(Settings.RequiredAudioCodecs.Value),
            FileQualityFilterOperationType.NOTIN => !codecs.FindInEnumerable(Settings.RequiredAudioCodecs.Value),
            _ => true
        };
    }

    private static bool CheckAudioStreamCount(SVR_VideoLocal aniFile)
    {
        var streamCount = aniFile?.MediaInfo?.AudioStreams.Count ?? -1;
        if (streamCount == -1)
        {
            return true;
        }

        var operationType = Settings.RequiredAudioStreamCount.Operator;
        return operationType switch
        {
            FileQualityFilterOperationType.EQUALS => streamCount == Settings.RequiredAudioStreamCount.Value,
            FileQualityFilterOperationType.GREATER_EQ => streamCount >= Settings.RequiredAudioStreamCount.Value,
            FileQualityFilterOperationType.LESS_EQ => streamCount <= Settings.RequiredAudioStreamCount.Value,
            _ => true
        };
    }

    private static bool CheckChaptered(SVR_VideoLocal aniFile)
    {
        return aniFile?.AniDBFile?.IsChaptered ?? (aniFile?.MediaInfo?.MenuStreams.Any() ?? false);
    }

    private static bool CheckDeprecated(AniDB_File aniFile)
    {
        return !(aniFile?.IsDeprecated ?? false);
    }

    private static bool CheckResolution(SVR_VideoLocal videoLocal)
    {
        var resTuple = GetResolutionInternal(videoLocal);
        var res = MediaInfoUtils.GetStandardResolution(resTuple);
        if (res == null)
        {
            return true;
        }

        var resArea = resTuple.Item1 * resTuple.Item2;

        var operationType = Settings.RequiredResolutions.Operator;
        switch (operationType)
        {
            case FileQualityFilterOperationType.EQUALS:
                return res.Equals(Settings.RequiredResolutions.Value.FirstOrDefault());
            case FileQualityFilterOperationType.GREATER_EQ:
                var keysGT = MediaInfoUtils.ResolutionArea.Keys.Where(a => resArea >= a).ToList();
                keysGT.AddRange(MediaInfoUtils.ResolutionArea43.Keys.Where(a => resArea >= a));
                var valuesGT = new List<string>();
                foreach (var key in keysGT)
                {
                    if (MediaInfoUtils.ResolutionArea.TryGetValue(key, out var value))
                    {
                        valuesGT.Add(value);
                    }

                    if (MediaInfoUtils.ResolutionArea43.TryGetValue(key, out var value1))
                    {
                        valuesGT.Add(value1);
                    }
                }

                if (valuesGT.FindInEnumerable(Settings.RequiredResolutions.Value))
                {
                    return true;
                }

                break;
            case FileQualityFilterOperationType.LESS_EQ:
                var keysLT = MediaInfoUtils.ResolutionArea.Keys.Where(a => resArea <= a).ToList();
                keysLT.AddRange(MediaInfoUtils.ResolutionArea43.Keys.Where(a => resArea <= a));
                var valuesLT = new List<string>();
                foreach (var key in keysLT)
                {
                    if (MediaInfoUtils.ResolutionArea.TryGetValue(key, out var value))
                    {
                        valuesLT.Add(value);
                    }

                    if (MediaInfoUtils.ResolutionArea43.TryGetValue(key, out var value1))
                    {
                        valuesLT.Add(value1);
                    }
                }

                if (valuesLT.FindInEnumerable(Settings.RequiredResolutions.Value))
                {
                    return true;
                }

                break;
            case FileQualityFilterOperationType.IN:
                return Settings.RequiredResolutions.Value.Contains(res);
            case FileQualityFilterOperationType.NOTIN:
                return !Settings.RequiredResolutions.Value.Contains(res);
        }

        return false;
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

    private static bool CheckVideoCodec(SVR_VideoLocal aniFile)
    {
        var codecs =
            aniFile?.MediaInfo?.media.track.Where(a => a.type == StreamType.Video)
                .Select(LegacyMediaUtils.TranslateCodec)
                .OrderBy(a => a).ToArray() ?? Array.Empty<string>();

        if (codecs.Length == 0)
        {
            return false;
        }

        var operationType = Settings.RequiredVideoCodecs.Operator;
        return operationType switch
        {
            FileQualityFilterOperationType.IN => Settings.RequiredVideoCodecs.Value.FindInEnumerable(codecs),
            FileQualityFilterOperationType.NOTIN => !Settings.RequiredVideoCodecs.Value.FindInEnumerable(codecs),
            _ => true
        };
    }

    #endregion

    #region Comparisons

    // -1 if oldFile is to be deleted, 0 if they are comparatively equal, 1 if the oldFile is better
    public static int CompareTo(this SVR_VideoLocal newFile, SVR_VideoLocal oldFile)
    {
        var oldEp = oldFile?.AniDBFile;
        var newEp = newFile?.AniDBFile;
        var result = 0;

        foreach (var type in Settings.PreferredTypes)
        {
            switch (type)
            {
                case FileQualityFilterType.AUDIOCODEC:
                    result = CompareAudioCodecTo(newFile, oldFile);
                    break;

                case FileQualityFilterType.AUDIOSTREAMCOUNT:
                    result = CompareAudioStreamCountTo(newFile, oldFile);
                    break;

                case FileQualityFilterType.CHAPTER:
                    result = CompareChapterTo(newFile, newEp, oldFile, oldEp);
                    break;

                case FileQualityFilterType.RESOLUTION:
                    result = CompareResolutionTo(newFile, oldFile);
                    break;

                case FileQualityFilterType.SOURCE:
                    if (IsNullOrUnknown(newEp) && IsNullOrUnknown(oldEp))
                    {
                        return 0;
                    }

                    if (IsNullOrUnknown(newEp))
                    {
                        return 1;
                    }

                    if (IsNullOrUnknown(oldEp))
                    {
                        return -1;
                    }

                    result = CompareSourceTo(newEp, oldEp);
                    break;

                case FileQualityFilterType.SUBGROUP:
                    if (IsNullOrUnknown(newEp) && IsNullOrUnknown(oldEp))
                    {
                        return 0;
                    }

                    if (IsNullOrUnknown(newEp))
                    {
                        return 1;
                    }

                    if (IsNullOrUnknown(oldEp))
                    {
                        return -1;
                    }

                    result = CompareSubGroupTo(newEp, oldEp);
                    break;

                case FileQualityFilterType.SUBSTREAMCOUNT:
                    result = CompareSubStreamCountTo(newFile, oldFile);
                    break;

                case FileQualityFilterType.VERSION:
                    if (newEp == null)
                    {
                        return 1;
                    }

                    if (oldEp == null)
                    {
                        return -1;
                    }

                    result = CompareVersionTo(newFile, oldFile);
                    break;

                case FileQualityFilterType.VIDEOCODEC:
                    result = CompareVideoCodecTo(newFile, oldFile);
                    break;
            }

            if (result != 0)
            {
                return result;
            }
        }

        return 0;
    }

    private static int CompareAudioCodecTo(SVR_VideoLocal newFile, SVR_VideoLocal oldFile)
    {
        var newCodecs = newFile?.MediaInfo?.AudioStreams?.Select(LegacyMediaUtils.TranslateCodec)
            .Where(a => a != null).OrderBy(a => a).ToArray() ?? Array.Empty<string>();
        var oldCodecs = oldFile?.MediaInfo?.AudioStreams?.Select(LegacyMediaUtils.TranslateCodec)
            .Where(a => a != null).OrderBy(a => a).ToArray() ?? Array.Empty<string>();
        // compare side by side, average codec quality would be vague and annoying, defer to number of audio tracks
        if (newCodecs.Length != oldCodecs.Length)
        {
            return 0;
        }

        for (var i = 0; i < Math.Min(newCodecs.Length, oldCodecs.Length); i++)
        {
            var newCodec = newCodecs[i];
            var oldCodec = oldCodecs[i];
            var newIndex = Settings.PreferredAudioCodecs.IndexOf(newCodec);
            var oldIndex = Settings.PreferredAudioCodecs.IndexOf(oldCodec);
            if (newIndex < 0 || oldIndex < 0)
            {
                continue;
            }

            var result = newIndex.CompareTo(oldIndex);
            if (result != 0)
            {
                return result;
            }
        }

        return 0;
    }

    private static int CompareAudioStreamCountTo(SVR_VideoLocal newFile, SVR_VideoLocal oldFile)
    {
        var newStreamCount = newFile?.MediaInfo?.AudioStreams.Count ?? 0;
        var oldStreamCount = oldFile?.MediaInfo?.AudioStreams.Count ?? 0;
        return oldStreamCount.CompareTo(newStreamCount);
    }

    private static int CompareChapterTo(SVR_VideoLocal newFile, AniDB_File newAniFile, SVR_VideoLocal oldFile,
        AniDB_File oldAniFile)
    {
        if ((newAniFile?.IsChaptered ?? (newFile?.MediaInfo?.MenuStreams.Any() ?? false)) &&
            !(oldAniFile?.IsChaptered ?? (oldFile?.MediaInfo?.MenuStreams.Any() ?? false)))
        {
            return -1;
        }

        if (!(newAniFile?.IsChaptered ?? (newFile?.MediaInfo?.MenuStreams.Any() ?? false)) &&
            (oldAniFile?.IsChaptered ?? (oldFile?.MediaInfo?.MenuStreams.Any() ?? false)))
        {
            return 1;
        }

        return (oldAniFile?.IsChaptered ?? (oldFile?.MediaInfo?.MenuStreams.Any() ?? false)).CompareTo(
            newAniFile?.IsChaptered ?? (newFile?.MediaInfo?.MenuStreams.Any() ?? false));
    }

    private static int CompareResolutionTo(SVR_VideoLocal newFile, SVR_VideoLocal oldFile)
    {
        var oldRes = GetResolution(oldFile);
        var newRes = GetResolution(newFile);

        switch (newRes)
        {
            case null when oldRes == null:
                return 0;
            case null:
                return 1;
        }

        if (oldRes == null)
        {
            return -1;
        }

        var res = Settings.PreferredResolutions.ToArray();
        switch (res.Contains(newRes))
        {
            case false when !res.Contains(oldRes):
                return 0;
            case false:
                return 1;
        }

        if (!res.Contains(oldRes))
        {
            return -1;
        }

        var newIndex = Array.IndexOf(res, newRes);
        var oldIndex = Array.IndexOf(res, oldRes);
        return newIndex.CompareTo(oldIndex);
    }

    private static int CompareSourceTo(AniDB_File newFile, AniDB_File oldFile)
    {
        var newSource = newFile.File_Source.ToLowerInvariant();
        if (FileQualityPreferences.SimplifiedSources.TryGetValue(newSource, out var source))
        {
            newSource = source;
        }

        var oldSource = oldFile.File_Source.ToLowerInvariant();
        if (FileQualityPreferences.SimplifiedSources.TryGetValue(oldSource, out var simplifiedSource))
        {
            oldSource = simplifiedSource;
        }

        var newIndex = Settings.PreferredSources.IndexOf(newSource);
        var oldIndex = Settings.PreferredSources.IndexOf(oldSource);
        return newIndex.CompareTo(oldIndex);
    }

    private static int CompareSubGroupTo(SVR_AniDB_File newFile, SVR_AniDB_File oldFile)
    {
        if (IsNullOrUnknown(newFile) || IsNullOrUnknown(oldFile))
        {
            return 0;
        }

        if (!Settings.PreferredSubGroups.Contains(newFile.Anime_GroupName.ToLowerInvariant()) &&
            !Settings.PreferredSubGroups.Contains(newFile.Anime_GroupNameShort.ToLowerInvariant()))
        {
            return 0;
        }

        if (!Settings.PreferredSubGroups.Contains(oldFile.Anime_GroupName.ToLowerInvariant()) &&
            !Settings.PreferredSubGroups.Contains(oldFile.Anime_GroupNameShort.ToLowerInvariant()))
        {
            return 0;
        }

        // The above ensures that _subgroups contains both, so no need to check for -1 in this case
        var newIndex = Settings.PreferredSubGroups.IndexOf(newFile.Anime_GroupName.ToLowerInvariant());
        if (newIndex == -1)
        {
            newIndex = Settings.PreferredSubGroups.IndexOf(newFile.Anime_GroupNameShort.ToLowerInvariant());
        }

        var oldIndex = Settings.PreferredSubGroups.IndexOf(oldFile.Anime_GroupName.ToLowerInvariant());
        if (oldIndex == -1)
        {
            oldIndex = Settings.PreferredSubGroups.IndexOf(oldFile.Anime_GroupNameShort.ToLowerInvariant());
        }

        return newIndex.CompareTo(oldIndex);
    }

    private static int CompareSubStreamCountTo(SVR_VideoLocal newFile, SVR_VideoLocal oldFile)
    {
        var newStreamCount = newFile?.MediaInfo?.TextStreams?.Count ?? 0;
        var oldStreamCount = oldFile?.MediaInfo?.TextStreams?.Count ?? 0;
        return oldStreamCount.CompareTo(newStreamCount);
    }

    private static int CompareVersionTo(SVR_VideoLocal newFile, SVR_VideoLocal oldFile)
    {
        var newAni = newFile?.AniDBFile;
        var oldAni = oldFile?.AniDBFile;
        if (IsNullOrUnknown(newAni) || IsNullOrUnknown(oldAni))return 0;
        if (!newAni.Anime_GroupName.Equals(oldAni.Anime_GroupName))return 0;
        if (!(newFile.MediaInfo?.VideoStream?.BitDepth).Equals(oldFile.MediaInfo?.VideoStream?.BitDepth))return 0;
        if (!string.Equals(newFile.MediaInfo?.VideoStream?.CodecID, oldFile.MediaInfo?.VideoStream?.CodecID))return 0;

        return oldAni.FileVersion.CompareTo(newAni.FileVersion);
    }

    private static int CompareVideoCodecTo(SVR_VideoLocal newLocal, SVR_VideoLocal oldLocal)
    {
        var newCodecs =
            newLocal?.MediaInfo?.media?.track?.Where(a => a?.type == StreamType.Video)
                .Select(LegacyMediaUtils.TranslateCodec).Where(a => a != null).OrderBy(a => a).ToArray() ??
            Array.Empty<string>();
        var oldCodecs =
            oldLocal?.MediaInfo?.media?.track?.Where(a => a?.type == StreamType.Video)
                .Select(LegacyMediaUtils.TranslateCodec).Where(a => a != null).OrderBy(a => a).ToArray() ??
            Array.Empty<string>();
        // compare side by side, average codec quality would be vague and annoying, defer to number of audio tracks
        if (newCodecs.Length != oldCodecs.Length)
        {
            return 0;
        }

        for (var i = 0; i < Math.Min(newCodecs.Length, oldCodecs.Length); i++)
        {
            var newCodec = newCodecs[i];
            var oldCodec = oldCodecs[i];
            var newIndex = Settings.PreferredVideoCodecs.IndexOf(newCodec);
            var oldIndex = Settings.PreferredVideoCodecs.IndexOf(oldCodec);
            if (newIndex < 0 || oldIndex < 0)
            {
                continue;
            }

            var result = newIndex.CompareTo(oldIndex);
            if (result != 0)
            {
                return result;
            }

            if (newLocal?.MediaInfo?.VideoStream?.BitDepth == null ||
                oldLocal?.MediaInfo?.VideoStream?.BitDepth == null)
            {
                continue;
            }

            switch (newLocal.MediaInfo.VideoStream.BitDepth)
            {
                case 8 when oldLocal.MediaInfo.VideoStream.BitDepth == 10:
                    return Settings.Prefer8BitVideo ? -1 : 1;
                case 10 when oldLocal.MediaInfo.VideoStream.BitDepth == 8:
                    return Settings.Prefer8BitVideo ? 1 : -1;
            }
        }

        return 0;
    }

    #endregion

    #region Information from Models (Operations that aren't simple)

    public static string GetResolution(SVR_VideoLocal videoLocal)
    {
        return MediaInfoUtils.GetStandardResolution(GetResolutionInternal(videoLocal));
    }

    public static string GetResolution(string res)
    {
        if (string.IsNullOrEmpty(res))
        {
            return null;
        }

        var parts = res.Split('x');
        if (parts.Length != 2)
        {
            return null;
        }

        if (!int.TryParse(parts[0], out var width))
        {
            return null;
        }

        if (!int.TryParse(parts[1], out var height))
        {
            return null;
        }

        return MediaInfoUtils.GetStandardResolution(new Tuple<int, int>(width, height));
    }

    private static Tuple<int, int> GetResolutionInternal(SVR_VideoLocal videoLocal)
    {
        var oldHeight = 0;
        var oldWidth = 0;
        var stream = videoLocal?.MediaInfo?.VideoStream;
        if (stream != null)
        {
            oldWidth = stream.Width;
            oldHeight = stream.Height;
        }

        if (oldHeight == 0 || oldWidth == 0)
        {
            return null;
        }

        return new Tuple<int, int>(oldWidth, oldHeight);
    }

    private static bool IsNullOrUnknown([NotNullWhen(false)][MaybeNullWhen(true)] SVR_AniDB_File file)
    {
        if (file == null)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(file.File_Source))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(file.Anime_GroupName))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(file.Anime_GroupNameShort))
        {
            return true;
        }

        if (file.Anime_GroupName.EqualsInvariantIgnoreCase("unknown"))
        {
            return true;
        }

        if (file.Anime_GroupNameShort.EqualsInvariantIgnoreCase("unknown"))
        {
            return true;
        }

        if (file.Anime_GroupName.EqualsInvariantIgnoreCase("raw"))
        {
            return true;
        }

        if (file.Anime_GroupNameShort.EqualsInvariantIgnoreCase("raw"))
        {
            return true;
        }

        if (file.File_Source.EqualsInvariantIgnoreCase("unknown"))
        {
            return true;
        }

        if (file.File_Source.EqualsInvariantIgnoreCase("raw"))
        {
            return true;
        }

        return false;
    }

    #endregion
}
