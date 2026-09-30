using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Stub;
using Shoko.Abstractions.Video.Media;
using Shoko.Server.Services;
using Shoko.Server.Utilities;

namespace Shoko.Server.MediaInfo;

/// <summary>
///   A chapter read from one entry of a menu stream, with one title per
///   language the file names it in. The titles are read from the file each
///   time and never stored.
/// </summary>
public partial class ChapterInfo : IChapterInfo
{
    #region Fields

    /// <summary>
    ///   The title of a chapter the file gives no name.
    /// </summary>
    private static readonly TitleStub _noTitle = new()
    {
        Source = MetadataSource.Shoko,
        Type = TitleType.Main,
        Language = TitleLanguage.Unknown,
        LanguageCode = "unk",
        Value = string.Empty,
    };

    #endregion

    #region Constructor

    /// <summary>
    ///   Creates a chapter.
    /// </summary>
    /// <param name="titles">The chapter's titles, the file's first one first. May be empty.</param>
    /// <param name="timestamp">Where the chapter starts.</param>
    public ChapterInfo(IReadOnlyList<ITitle> titles, TimeSpan timestamp)
    {
        Titles = titles;
        Timestamp = timestamp;
    }

    #endregion

    #region IChapterInfo Implementation

    /// <inheritdoc />
    public string Title => (PreferredTitle ?? DefaultTitle).Value;

    /// <inheritdoc />
    public ITitle DefaultTitle => Titles.Count > 0 ? Titles[0] : _noTitle;

    /// <inheritdoc />
    public ITitle? PreferredTitle => ChoosePreferredTitle(Titles, [.. Languages.PreferredEpisodeNamingLanguages.Select(language => language.Language)]);

    /// <inheritdoc />
    public IReadOnlyList<ITitle> Titles { get; }

    /// <inheritdoc />
    public TimeSpan Timestamp { get; }

    #endregion

    #region Parsing

    /// <summary>
    ///   A language prefix as MediaInfo writes one before a chapter name: a
    ///   lowercase ISO 639 code, or a BCP 47 tag built on one, then a colon.
    /// </summary>
    [GeneratedRegex(@"^(?<tag>(?<language>[a-z]{2,3})(?:-[A-Za-z]{4})?(?:-(?<region>[A-Za-z]{2}|[0-9]{3}))?):", RegexOptions.CultureInvariant)]
    private static partial Regex LanguagePrefix();

    /// <summary>
    ///   The <c> - </c> MediaInfo puts between a chapter's names, when a
    ///   language prefix follows it.
    /// </summary>
    [GeneratedRegex(@" - (?=[a-z]{2,3}(?:-[A-Za-z]{4})?(?:-(?:[A-Za-z]{2}|[0-9]{3}))?:)", RegexOptions.CultureInvariant)]
    private static partial Regex NameSeparator();

    /// <summary>
    ///   Reads a chapter from one entry of a menu stream.
    /// </summary>
    /// <param name="key">
    ///   The entry's name, the chapter's start as <c>_HH_MM_SS_mmm</c>.
    /// </param>
    /// <param name="value">
    ///   The entry's value: for Matroska every name of the chapter as
    ///   <c>language:name</c>, joined by <c> - </c>, with no prefix for an
    ///   undetermined language; for other containers the plain name.
    /// </param>
    /// <returns>The chapter, or <c>null</c> when the name is not a start time.</returns>
    internal static ChapterInfo? Parse(string? key, string? value)
    {
        if (string.IsNullOrEmpty(key))
            return null;

        var parts = key[1..].Split('_');
        if (parts.Length < 4)
            return null;

        var time = $"{parts[0]}:{parts[1]}:{parts[2]}.{parts[3]}";
        if (!TimeSpan.TryParse(time, CultureInfo.InvariantCulture, out var timestamp))
            return null;

        // MediaInfo writes the start time as the value of a chapter with no name.
        return new(value?.Trim() == time ? [] : ParseTitles(value), timestamp);
    }

    /// <summary>
    ///   Reads the titles of a chapter from a menu entry's value. A prefix is
    ///   only read as a language when it looks like one, and names are only
    ///   split where a language prefix follows the separator.
    /// </summary>
    /// <param name="value">The entry's value, as described on <see cref="Parse"/>.</param>
    /// <returns>
    ///   One title per language, in the file's order: the first of type main
    ///   and the rest official, from <see cref="MetadataSource.Shoko"/>, since
    ///   the server read them from the file. A name without a language gets
    ///   <c>unk</c>. Empty names and later names in a language already seen
    ///   are left out. An unrecognised language code is reported.
    /// </returns>
    internal static IReadOnlyList<ITitle> ParseTitles(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return [];

        var titles = new List<ITitle>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in NameSeparator().Split(value))
        {
            var match = LanguagePrefix().Match(part);
            var name = (match.Success ? part[match.Length..] : part).Trim();
            var tag = match.Success ? match.Groups["tag"].Value : null;
            var language = tag is null ? TitleLanguage.Unknown : tag.GetTitleLanguage();
            if (name.Length is 0 || !seen.Add(tag ?? "unk"))
                continue;

            titles.Add(new TitleStub
            {
                Source = MetadataSource.Shoko,
                Type = titles.Count is 0 ? TitleType.Main : TitleType.Official,
                Language = language,
                LanguageCode = match.Success ? match.Groups["language"].Value : "unk",
                CountryCode = match.Success && match.Groups["region"].Success ? match.Groups["region"].Value.ToUpperInvariant() : null,
                Value = name,
            });
        }

        return titles;
    }

    /// <summary>
    ///   Chooses a chapter's preferred title the way the core chooses any
    ///   title, walking the languages given over the chapter's own source.
    /// </summary>
    /// <param name="titles">The chapter's titles.</param>
    /// <param name="languages">The languages to walk, best first; <c>x-main</c> gives the file's first title.</param>
    /// <returns>The title, or <c>null</c> when none is in the languages given.</returns>
    internal static ITitle? ChoosePreferredTitle(IReadOnlyList<ITitle> titles, IReadOnlyList<TitleLanguage> languages)
        => titles.Count is 0 ? null : TextChooser.ChooseTitle(titles, new(languages, [MetadataSource.Shoko], UseSynonyms: false, RankGeneric: false));

    #endregion
}
