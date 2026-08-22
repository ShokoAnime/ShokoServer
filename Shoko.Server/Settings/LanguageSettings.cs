using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Config.Attributes;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.UI.Attributes;

namespace Shoko.Server.Settings;

public class LanguageSettings
{
    /// <summary>
    /// Use synonyms when selecting the preferred language from AniDB.
    /// </summary>
    public bool UseSynonyms { get; set; } = false;

    private List<string> _seriesTitleLanguageOrder = ["x-main"];

    /// <summary>
    /// Series / group title language preference order.
    /// </summary>
    [RequiresRestart]
    public List<string> SeriesTitleLanguageOrder
    {
        get => _seriesTitleLanguageOrder;
        set => _seriesTitleLanguageOrder = value.Where(s => !string.IsNullOrEmpty(s)).ToList();
    }

    private List<MetadataSource> _seriesTitleSourceOrder = [MetadataSource.AniDB, MetadataSource.TMDB];

    /// <summary>
    /// Series / group title source preference order.
    /// </summary>
    [RequiresRestart]
    public List<MetadataSource> SeriesTitleSourceOrder
    {
        get => _seriesTitleSourceOrder;
        set => _seriesTitleSourceOrder = value.Distinct().ToList();
    }

    private List<string> _episodeLanguagePreference = ["en"];

    /// <summary>
    /// Episode / season title language preference order.
    /// </summary>
    [RequiresRestart]
    public List<string> EpisodeTitleLanguageOrder
    {
        get => _episodeLanguagePreference;
        set => _episodeLanguagePreference = value.Where(s => !string.IsNullOrEmpty(s)).ToList();
    }

    private List<MetadataSource> _episodeTitleSourceOrder = [MetadataSource.TMDB, MetadataSource.AniDB];

    /// <summary>
    /// Episode / season title source preference order.
    /// </summary>
    [RequiresRestart]
    public List<MetadataSource> EpisodeTitleSourceOrder
    {
        get => _episodeTitleSourceOrder;
        set => _episodeTitleSourceOrder = value.Distinct().ToList();
    }

    private List<string> _descriptionLanguagePreference = ["en"];

    /// <summary>
    /// Description language preference order.
    /// </summary>
    [RequiresRestart]
    public List<string> DescriptionLanguageOrder
    {
        get => _descriptionLanguagePreference;
        set => _descriptionLanguagePreference = value.Where(s => !string.IsNullOrEmpty(s)).ToList();
    }

    private List<MetadataSource> _descriptionSourceOrder = [MetadataSource.TMDB, MetadataSource.AniDB];

    /// <summary>
    /// Description source preference order.
    /// </summary>
    [RequiresRestart]
    public List<MetadataSource> DescriptionSourceOrder
    {
        get => _descriptionSourceOrder;
        set => _descriptionSourceOrder = value.Distinct().ToList();
    }
}
