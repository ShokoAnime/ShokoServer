using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Plugin.Abstractions.DataModels;

#nullable enable
namespace Shoko.Server.Utilities;

public static class Languages
{
    private static readonly TitleLanguage[] _invalidLanguages = [TitleLanguage.Unknown, TitleLanguage.None];

    public static List<NamingLanguage> AllNamingLanguages =>
        Enum.GetValues<TitleLanguage>()
            .Except(_invalidLanguages)
            .Select(l => new NamingLanguage(l))
            .ToList();

    private static List<NamingLanguage>? _preferredNamingLanguages;

    private static readonly object _lockObj = new();

    public static List<NamingLanguage> PreferredNamingLanguages
    {
        get
        {
            if (_preferredNamingLanguages is not null)
                return _preferredNamingLanguages;

            lock (_lockObj)
            {
                if (_preferredNamingLanguages is not null)
                    return _preferredNamingLanguages;

                _preferredNamingLanguages = null;
                var preference = Utils.SettingsProvider.GetSettings().Language.SeriesTitleLanguageOrder ?? [];
                _preferredNamingLanguages = preference
                    .Where(l => !string.IsNullOrEmpty(l))
                    .Select(l => new NamingLanguage(l))
                    .ExceptBy(_invalidLanguages, l => l.Language)
                    .ToList();

                return _preferredNamingLanguages;
            }
        }
        set
        {
            if (Utils.SettingsProvider is null)
                return;

            lock (_lockObj)
            {
                var preference = Utils.SettingsProvider.GetSettings().Language.SeriesTitleLanguageOrder ?? [];
                _preferredNamingLanguages = preference
                    .Where(l => !string.IsNullOrEmpty(l))
                    .Select(l => new NamingLanguage(l))
                    .ExceptBy(_invalidLanguages, l => l.Language)
                    .ToList();
            }
        }
    }

    private static List<NamingLanguage>? _preferredEpisodeNamingLanguages;

    public static List<NamingLanguage> PreferredEpisodeNamingLanguages
    {
        get
        {
            if (_preferredEpisodeNamingLanguages is not null)
                return _preferredEpisodeNamingLanguages;
            lock (_lockObj)
            {
                if (_preferredEpisodeNamingLanguages is not null)
                    return _preferredEpisodeNamingLanguages;

                var preference = Utils.SettingsProvider.GetSettings().Language.EpisodeTitleLanguageOrder ?? [];
                _preferredEpisodeNamingLanguages = preference
                    .Where(l => !string.IsNullOrEmpty(l))
                    .Select(l => new NamingLanguage(l))
                    .ExceptBy(_invalidLanguages, l => l.Language)
                    .ToList();

                return _preferredEpisodeNamingLanguages;
            }
        }
        set
        {
            if (Utils.SettingsProvider is null)
                return;

            lock (_lockObj)
            {
                var preference = Utils.SettingsProvider.GetSettings().Language.EpisodeTitleLanguageOrder ?? [];
                _preferredEpisodeNamingLanguages = preference
                    .Where(l => !string.IsNullOrEmpty(l))
                    .Select(l => new NamingLanguage(l))
                    .ExceptBy(_invalidLanguages, l => l.Language)
                    .ToList();
            }
        }
    }

    private static List<NamingLanguage>? _preferredDescriptionNamingLanguages;

    public static List<NamingLanguage> PreferredDescriptionNamingLanguages
    {
        get
        {
            if (_preferredDescriptionNamingLanguages is not null)
                return _preferredDescriptionNamingLanguages;
            lock (_lockObj)
            {
                if (_preferredDescriptionNamingLanguages is not null)
                    return _preferredDescriptionNamingLanguages;

                var preference = Utils.SettingsProvider.GetSettings().Language.DescriptionLanguageOrder ?? [];
                _preferredDescriptionNamingLanguages = preference
                    .Where(l => !string.IsNullOrEmpty(l))
                    .Select(l => new NamingLanguage(l))
                    .ExceptBy(_invalidLanguages, l => l.Language)
                    .ToList();

                return _preferredDescriptionNamingLanguages;
            }
        }
        set
        {
            if (Utils.SettingsProvider is null)
                return;

            lock (_lockObj)
            {
                var preference = Utils.SettingsProvider.GetSettings().Language.DescriptionLanguageOrder ?? [];
                _preferredDescriptionNamingLanguages = preference
                    .Where(l => !string.IsNullOrEmpty(l))
                    .Select(l => new NamingLanguage(l))
                    .ExceptBy(_invalidLanguages, l => l.Language)
                    .ToList();
            }
        }
    }
}
