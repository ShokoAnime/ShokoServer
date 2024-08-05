using System.Collections.Generic;
using System.Linq;
using Shoko.Models.Enums;
using Shoko.Server.Utilities;

#nullable enable
namespace Shoko.Server.Settings;

public class LanguageSettings
{
    /// <summary>
    /// Use synonyms when selecting the preferred language from AniDB.
    /// </summary>
    public bool UseSynonyms { get; set; } = false;

    private List<string> _seriesTitleLanguageOrder = [];

    /// <summary>
    /// Series / group title language preference order.
    /// </summary>
    public List<string> SeriesTitleLanguageOrder
    {
        get => _seriesTitleLanguageOrder;
        set
        {
            _seriesTitleLanguageOrder = value.Where(s => !string.IsNullOrEmpty(s)).ToList();
            Languages.PreferredNamingLanguages = null;
            Languages.PreferredNamingLanguageNames = null;
        }
    }

    private List<DataSourceType> _seriesTitleSourceOrder = [DataSourceType.AniDB, DataSourceType.TMDB];

    /// <summary>
    /// Series / group title source preference order.
    /// </summary>
    public List<DataSourceType> SeriesTitleSourceOrder
    {
        get => _seriesTitleSourceOrder;
        set => _seriesTitleSourceOrder = value.Distinct().ToList();
    }

    private List<string> _episodeLanguagePreference = ["en"];

    /// <summary>
    /// Episode / season title language preference order.
    /// </summary>
    public List<string> EpisodeTitleLanguageOrder
    {
        get => _episodeLanguagePreference;
        set
        {
            _episodeLanguagePreference = value.Where(s => !string.IsNullOrEmpty(s)).ToList();
            Languages.PreferredEpisodeNamingLanguages = null;
        }
    }

    private List<DataSourceType> _episodeTitleSourceOrder = [DataSourceType.TMDB, DataSourceType.TvDB, DataSourceType.AniDB];

    /// <summary>
    /// Episode / season title source preference order.
    /// </summary>
    public List<DataSourceType> EpisodeTitleSourceOrder
    {
        get => _episodeTitleSourceOrder;
        set => _episodeTitleSourceOrder = value.Distinct().ToList();
    }

    private List<string> _descriptionLanguagePreference = ["en"];

    /// <summary>
    /// Description language preference order.
    /// </summary>
    public List<string> DescriptionLanguageOrder
    {
        get => _descriptionLanguagePreference;
        set
        {
            _descriptionLanguagePreference = value.Where(s => !string.IsNullOrEmpty(s)).ToList();
            Languages.PreferredDescriptionNamingLanguages = null;
        }
    }

    private List<DataSourceType> _descriptionSourceOrder = [DataSourceType.TMDB, DataSourceType.TvDB, DataSourceType.AniDB];

    /// <summary>
    /// Description source preference order.
    /// </summary>
    public List<DataSourceType> DescriptionSourceOrder
    {
        get => _descriptionSourceOrder;
        set => _descriptionSourceOrder = value.Distinct().ToList();
    }
}
