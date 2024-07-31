using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Plugin.Abstractions.DataModels;

namespace Shoko.Server.Utilities;

public static class Languages
{
    private static readonly TitleLanguage[] _invalidLanguages = [TitleLanguage.Unknown, TitleLanguage.None];

    public static List<NamingLanguage> AllNamingLanguages =>
        Enum.GetValues<TitleLanguage>()
            .Except(_invalidLanguages)
            .Select(l => new NamingLanguage(l))
            .ToList();

    private static List<NamingLanguage> _preferredNamingLanguages;

    public static List<NamingLanguage> PreferredNamingLanguages
    {
        get
        {
            if (_preferredNamingLanguages != null)
                return _preferredNamingLanguages;

            var preference = Utils.SettingsProvider.GetSettings().Language.SeriesTitleLanguageOrder ?? new();
            return _preferredNamingLanguages = preference
                .Where(l => !string.IsNullOrEmpty(l))
                .Select(l => new NamingLanguage(l))
                .ExceptBy(_invalidLanguages, l => l.Language)
                .ToList();
        }
        set => _preferredNamingLanguages = value;
    }

    private static List<TitleLanguage> _preferredNamingLanguageNames;

    public static List<TitleLanguage> PreferredNamingLanguageNames
    {
        get
        {
            if (_preferredNamingLanguageNames != null) return _preferredNamingLanguageNames;
            _preferredNamingLanguageNames = PreferredNamingLanguages.Select(a => a.Language).ToList();
            return _preferredNamingLanguageNames;
        }
        set => _preferredNamingLanguageNames = value;
    }

    private static List<NamingLanguage> _preferredEpisodeNamingLanguages;

    public static List<NamingLanguage> PreferredEpisodeNamingLanguages
    {
        get => _preferredEpisodeNamingLanguages ??= (Utils.SettingsProvider.GetSettings().Language.EpisodeTitleLanguageOrder ?? [])
            .Where(l => !string.IsNullOrEmpty(l))
            .Select(l => new NamingLanguage(l))
            .ExceptBy(_invalidLanguages, l => l.Language)
            .ToList();
        set => _preferredEpisodeNamingLanguages = value;
    }

    private static List<NamingLanguage> _preferredDescriptionNamingLanguages;

    public static List<NamingLanguage> PreferredDescriptionNamingLanguages
    {
        get => _preferredDescriptionNamingLanguages ??= (Utils.SettingsProvider.GetSettings().Language.DescriptionLanguageOrder ?? [])
            .Where(l => !string.IsNullOrEmpty(l))
            .Select(l => new NamingLanguage(l))
            .ExceptBy(_invalidLanguages, l => l.Language)
            .ToList();
        set => _preferredDescriptionNamingLanguages = value;
    }
}
