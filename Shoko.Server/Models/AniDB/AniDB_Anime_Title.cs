using Shoko.Abstractions.Enums;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;

# nullable enable
namespace Shoko.Server.Models.AniDB;

public class AniDB_Anime_Title : ITitle
{
    public int AniDB_Anime_TitleID { get; set; }

    public int AnimeID { get; set; }

    /// <summary>
    /// The title type.
    /// </summary>
    public TitleType TitleType { get; set; }

    /// <summary>
    /// The language.
    /// </summary>
    public TitleLanguage Language { get; set; }

    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// The language code.
    /// </summary>
    public string LanguageCode
    {
        get => Language.GetString();
        set => Language = value.GetTitleLanguage();
    }

    public bool Equals(IText? other)
        => IText.Equals(this, other);

    public bool Equals(ITitle? other)
        => ITitle.Equals(this, other);

    TitleType ITitle.Type => TitleType;

    string? IText.CountryCode => null;

    string IText.Value => Title;

    DataSource IMetadata.Source => DataSource.AniDB;
}
