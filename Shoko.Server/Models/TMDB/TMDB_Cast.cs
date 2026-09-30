using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Repositories;

namespace Shoko.Server.Models.TMDB;

/// <summary>
/// Cast member for an episode.
/// </summary>
public abstract class TMDB_Cast : ICast
{
    #region Properties

    /// <summary>
    /// TMDB Person ID for the cast member.
    /// </summary>
    public int TmdbPersonID { get; set; }

    /// <summary>
    /// TMDB Parent ID for the production job.
    /// </summary>
    public abstract int TmdbParentID { get; }

    /// <summary>
    /// The kind of TMDB entry the parent is.
    /// </summary>
    public abstract MetadataEntityType ParentType { get; }

    /// <summary>
    /// TMDB Credit ID for the acting job.
    /// </summary>
    public string TmdbCreditID { get; set; } = string.Empty;

    /// <summary>
    /// Character name.
    /// </summary>
    public string CharacterName { get; set; } = string.Empty;

    /// <summary>
    /// Ordering.
    /// </summary>
    public int Ordering { get; set; }

    #endregion

    #region Methods

    public TMDB_Person? GetTmdbPerson() =>
        RepoFactory.TMDB_Person.GetByTmdbPersonID(TmdbPersonID);

    public abstract IMetadata? GetTmdbParent();

    /// <summary>
    /// TMDB only lists the original-language cast, so every role is in the
    /// original language of the show or movie.
    /// </summary>
    public TitleLanguage Language => GetTmdbParent() switch
    {
        TMDB_Show show => show.OriginalLanguage,
        TMDB_Movie movie => movie.OriginalLanguage,
        TMDB_Episode episode => RepoFactory.TMDB_Show.GetByTmdbShowID(episode.TmdbShowID)?.OriginalLanguage ?? TitleLanguage.Unknown,
        _ => TitleLanguage.Unknown,
    };

    public string LanguageCode => Language.GetString();

    #endregion

    #region ICast Implementation

    MetadataSource ICast.Source => MetadataSource.TMDB;

    MetadataGuid? ICast.CreatorID => new(MetadataSource.TMDB, MetadataEntityType.Creator, TmdbPersonID.ToString());

    MetadataGuid? ICast.CharacterID => null;

    MetadataGuid ICast.ParentID => new(MetadataSource.TMDB, ParentType, TmdbParentID.ToString());

    string ICast.Name => CharacterName;

    string? ICast.OriginalName => null;

    string? ICast.Description => null;

    string? ICast.DubGroup => null;

    CastRoleType ICast.RoleType => CastRoleType.None;

    IMetadata? ICast.Parent => GetTmdbParent();

    ICharacter? ICast.Character => null;

    ICreator? ICast.Creator => GetTmdbPerson();

    #endregion
}
