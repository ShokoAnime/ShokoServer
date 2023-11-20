using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Plugin.Abstractions.DataModels;

namespace Shoko.Server.Utilities;

public static class Languages
{
    public static List<NamingLanguage> AllNamingLanguages =>
        Enum.GetValues<TitleLanguage>().Select(l => new NamingLanguage(l)).ToList();

    private static List<NamingLanguage> _preferredNamingLanguages;

    public static List<NamingLanguage> PreferredNamingLanguages
    {
        get
        {
            if (_preferredNamingLanguages != null)
                return _preferredNamingLanguages;

            var preference = Utils.SettingsProvider.GetSettings().LanguagePreference ?? new();
            return _preferredNamingLanguages = preference
                .Where(l => !string.IsNullOrEmpty(l))
                .Select(l => new NamingLanguage(l))
                .Where(l => l.Language != TitleLanguage.Unknown)
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
        get
        {
            if (_preferredEpisodeNamingLanguages != null)
                return _preferredEpisodeNamingLanguages;

            var preference = Utils.SettingsProvider.GetSettings().EpisodeLanguagePreference ?? new();
            return _preferredEpisodeNamingLanguages = preference
                .Where(l => !string.IsNullOrEmpty(l))
                .Select(l => new NamingLanguage(l))
                .Where(l => l.Language != TitleLanguage.Unknown)
                .ToList();
        }
        set => _preferredEpisodeNamingLanguages = value;
    }
}
