using System;
using Shoko.Abstractions.Enums;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;

# nullable enable
namespace Shoko.Server.Models.AniDB;

public class AniDB_Episode_Title : IEquatable<AniDB_Episode_Title>, ITitle
{
    public int AniDB_Episode_TitleID { get; set; }

    public int AniDB_EpisodeID { get; set; }

    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// The language.
    /// </summary>
    /// <value></value>
    public TitleLanguage Language { get; set; }

    /// <summary>
    /// The language code.
    /// </summary>
    /// <value></value>
    public string LanguageCode
    {
        get => Language.GetString();
        set => Language = value.GetTitleLanguage();
    }

    public bool Equals(AniDB_Episode_Title? other)
        => other is not null &&
        AniDB_EpisodeID == other.AniDB_EpisodeID &&
        Language == other.Language &&
        Title == other.Title;

    public bool Equals(IText? other)
        => IText.Equals(this, other);

    public bool Equals(ITitle? other)
        => ITitle.Equals(this, other);

    public override bool Equals(object? obj)
        => obj is not null && (ReferenceEquals(this, obj) || Equals(obj as AniDB_Episode_Title));

    public override int GetHashCode()
        => HashCode.Combine(AniDB_EpisodeID, Language, Title);

    TitleType ITitle.Type => Language is TitleLanguage.English ? TitleType.Main : TitleType.None;

    string? IText.CountryCode => null;

    string IText.Value => Title;

    DataSource IMetadata.Source => DataSource.AniDB;
}
