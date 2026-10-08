using Shoko.Server.MediaInfo;
using Shoko.Server.MediaInfo.Subtitles;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories;

namespace Shoko.Server.Databases;

public partial class DatabaseFixes
{
    #region External Subtitle Languages | Steps

    /// <summary>
    ///   Re-derives the language of external subtitle streams whose stored
    ///   language is not a language, from the filename kept with the stream.
    /// </summary>
    /// <remarks>
    ///   Until the filename parsing was fixed, the text between the last dot
    ///   of the filename and the extension was stored as the language, such as
    ///   <c> 09 Title (1080p) [ABCD1234]_eng</c> for <c>_eng.ass</c>, and the
    ///   stored media info kept it.
    /// </remarks>
    public static void RepairExternalSubtitleLanguages()
    {
        var repaired = 0;
        foreach (var video in RepoFactory.VideoLocal.GetAll())
        {
            // An outdated media info is rebuilt from scratch on its own.
            if (video.MediaInfo is not { } mediaInfo || video.MediaVersion < VideoLocal.MEDIA_VERSION)
                continue;

            var changed = false;
            foreach (var stream in mediaInfo.TextStreams)
                changed |= RepairExternalSubtitleLanguage(stream);

            if (!changed)
                continue;

            RepoFactory.VideoLocal.Save(video, false);
            repaired++;
        }

        _logger.Info("Repaired the external subtitle languages of {Count} videos.", repaired);
    }

    internal static bool RepairExternalSubtitleLanguage(TextStream stream)
    {
        if (!stream.External || string.IsNullOrEmpty(stream.Filename) || string.IsNullOrEmpty(stream.Language) || SubtitleHelper.IsLanguageTag(stream.Language))
            return false;

        var language = SubtitleHelper.GetLanguageFromFilename(stream.Filename);
        if (language == stream.Language)
            return false;

        var mapping = language is null ? null : MediaInfoUtility.GetLanguageMapping(language);
        stream.Language = language;
        stream.LanguageCode = mapping?.Item1;
        stream.LanguageName = mapping?.Item2;
        return true;
    }

    #endregion
}
