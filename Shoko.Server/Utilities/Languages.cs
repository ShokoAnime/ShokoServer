using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Server.Settings;

namespace Shoko.Server.Utilities;

public class Languages
{
    public static List<NamingLanguage> AllNamingLanguages =>
        Enum.GetValues<TitleLanguage>().Select(l => new NamingLanguage(l)).ToList();

    private static List<NamingLanguage> _preferredNamingLanguages;
    public static List<NamingLanguage> PreferredNamingLanguages
    {
        get
        {
            var preference = ServerSettings.Instance.LanguagePreference ?? new List<string>();
            if (_preferredNamingLanguages != null) return _preferredNamingLanguages;
            return _preferredNamingLanguages = preference
                .Select(l => new NamingLanguage(l))
                .Where(l => l.Language != TitleLanguage.Unknown)
                .ToList();
        }
        set => _preferredNamingLanguages = value;
    }

    public static List<NamingLanguage> PreferredEpisodeNamingLanguages
    {
        get
        {
            var preference = ServerSettings.Instance.EpisodeLanguagePreference?.Split(',') ?? new string[] { };
            return preference
                .Select(l => new NamingLanguage(l))
                .Where(l => l.Language != TitleLanguage.Unknown)
                .ToList();
        }
    }
}
