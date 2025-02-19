using System.ComponentModel.DataAnnotations.Schema;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Extensions;
using Shoko.Server.Server;

#nullable enable
namespace Shoko.Server.Models.TMDB;

public abstract class TMDB_Overview
{
    public int TMDB_OverviewID { get; set; }

    public int ParentID { get; set; }

    [NotMapped] // Discriminators cannot be mapped. They are automatically set from the type
    public ForeignEntityType ParentType { get; set; }

    [NotMapped]
    public TitleLanguage Language
    {
        get => string.IsNullOrEmpty(LanguageCode) ? TitleLanguage.None : LanguageCode.GetTitleLanguage();
    }

    /// <summary>
    /// ISO 639-1 alpha-2 language code.
    /// </summary>
    public string LanguageCode { get; set; } = string.Empty;

    /// <summary>
    /// ISO 3166-1 alpha-2 country code.
    /// </summary>
    public string CountryCode { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;
}

public class TMDB_Overview_Season : TMDB_Overview;
public class TMDB_Overview_TVShow : TMDB_Overview;
public class TMDB_Overview_Movie : TMDB_Overview;
public class TMDB_Overview_Episode : TMDB_Overview;
public class TMDB_Overview_Collection : TMDB_Overview;
public class TMDB_Overview_Person : TMDB_Overview;
