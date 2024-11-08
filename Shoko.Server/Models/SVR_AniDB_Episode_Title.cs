using Shoko.Models.Server;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Extensions;

namespace Shoko.Server.Models;

public class SVR_AniDB_Episode_Title
{
    public int AniDB_Episode_TitleID { get; set; }

    public int AniDB_EpisodeID { get; set; }

    public string Title { get; set; }

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

    protected bool Equals(SVR_AniDB_Episode_Title other)
    {
        return AniDB_EpisodeID == other.AniDB_EpisodeID && Language == other.Language && Title == other.Title;
    }

    public override bool Equals(object obj)
    {
        if (obj is null)
        {
            return false;
        }

        if (ReferenceEquals(this, obj))
        {
            return true;
        }

        if (obj.GetType() != GetType())
        {
            return false;
        }

        return Equals((SVR_AniDB_Episode_Title)obj);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = AniDB_EpisodeID;
            hashCode = (hashCode * 397) ^ Language.GetHashCode();
            hashCode = (hashCode * 397) ^ (Title != null ? Title.GetHashCode() : 0);
            return hashCode;
        }
    }
}
