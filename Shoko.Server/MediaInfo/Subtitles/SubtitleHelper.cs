using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Shoko.Abstractions.Extensions;
using Shoko.Server.Extensions;
using Shoko.Server.Utilities;

// ReSharper disable StringLiteralTypo
// ReSharper disable InconsistentNaming

namespace Shoko.Server.MediaInfo.Subtitles;

public static class SubtitleHelper
{
    private static List<ISubtitles>? SubtitleImplementations;

    public static List<TextStream> GetSubtitleStreams(string? path)
    {
        if (string.IsNullOrEmpty(path)) return [];

        var directoryName = Path.GetDirectoryName(path);
        if (string.IsNullOrEmpty(directoryName)) return [];
        if (!Directory.Exists(directoryName)) return [];

        SubtitleImplementations ??= InitImplementations();
        var directory = new DirectoryInfo(directoryName);
        var basename = Path.GetFileNameWithoutExtension(path);
        var streams = new List<TextStream>();

        foreach (var file in directory.EnumerateFiles())
        {
            // Make sure it's actually the subtitle for this video file
            if (!file.Name.StartsWith(basename)) continue;

            // Get streams for each implementation
            SubtitleImplementations.Where(implementation => implementation.IsSubtitleFile(file.Extension))
                .SelectMany(implementation => implementation.GetStreams(file)).ForEach(streams.Add);
        }

        return streams;
    }

    private static List<ISubtitles> InitImplementations()
    {
        try
        {
            return ReflectionUtils.ScannableAssemblies()
                .SelectMany(x => x.GetTypes())
                .Where(x => typeof(ISubtitles).IsAssignableFrom(x) && !x.IsInterface && !x.IsAbstract)
                .Select(type => (ISubtitles)Activator.CreateInstance(type)!)
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    public static string? GetLanguageFromFilename(string path)
    {
        var lastSeparator = path.Trim(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).LastIndexOfAny(
        [
            Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar
        ]);
        var filename = path[(lastSeparator + 1)..];

        // sub format of filename_eng.srt or filename_eng_1.srt (duplicate-track suffix)
        if (GetTrailingLanguageSuffix(Path.GetFileNameWithoutExtension(filename)) is { } underscoreLang)
            return NormalizeLanguageTag(underscoreLang);

        // sub format of filename.eng.srt
        var parts = filename.Split('.');
        // if there aren't 3 parts, then it's not in the format for this to work
        if (parts.Length < 3) return null;

        // length - 1 is last, so - 2 is second to last
        var lang = parts[^2];

        return lang.Length switch
        {
            2 => lang,
            3 => MediaInfoUtility.GetLanguageFromCode(lang) ?? lang,
            // an unrecognised segment isn't a language — don't pass raw text through
            _ => MediaInfoUtility.GetLanguageFromName(lang),
        };
    }

    private static string? GetTrailingLanguageSuffix(string basename)
    {
        var parts = basename.Split('_');
        if (parts.Length < 2) return null;

        var last = parts[^1];
        // strip a duplicate-track disambiguator, e.g. "..._eng_1"
        if (int.TryParse(last, out _))
        {
            if (parts.Length < 3) return null;
            last = parts[^2];
        }

        return IsLanguageTag(last) ? last : null;
    }

    // "eng", "en", or either with a region such as "pt-BR", "spa-ES" or "es-419".
    internal static bool IsLanguageTag(string tag)
    {
        var dash = tag.IndexOf('-');
        var language = dash < 0 ? tag : tag[..dash];
        if (language.Length is not (2 or 3) || !language.All(char.IsAsciiLetter))
            return false;

        if (dash < 0)
            return true;

        var region = tag[(dash + 1)..];
        if (!(region.Length == 2 && region.All(char.IsAsciiLetter) || region.Length == 3 && region.All(char.IsAsciiDigit)))
            return false;

        // Rules out suffixes such as "OP-NC", whose first part is not a language.
        var normalized = NormalizeLanguageTag(tag);
        return normalized[..normalized.IndexOf('-')].TryGetTitleLanguage(out _);
    }

    private static string NormalizeLanguageTag(string tag)
    {
        var dash = tag.IndexOf('-');
        var language = dash < 0 ? tag : tag[..dash];
        if (language.Length == 3)
            language = MediaInfoUtility.GetLanguageFromCode(language) ?? language;

        return dash < 0 ? language : language + tag[dash..];
    }
}
