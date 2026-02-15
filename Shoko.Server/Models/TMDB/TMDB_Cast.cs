using Shoko.Abstractions.Enums;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Server.Repositories;

#nullable enable
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

    public abstract IMetadata<int>? GetTmdbParent();

    #endregion

    #region IMetadata Implementation

    string IMetadata<string>.ID => TmdbCreditID;

    DataSource IMetadata.Source => DataSource.TMDB;

    #endregion

    #region IWithPortraitImage Implementation

    IImage? IWithPortraitImage.PortraitImage => null;

    #endregion

    #region ICast Implementation

    int? ICast.CreatorID => TmdbPersonID;

    int? ICast.CharacterID => null;

    int ICast.ParentID => TmdbParentID;

    string ICast.Name => CharacterName;

    string? ICast.OriginalName => null;

    string? ICast.Description => null;

    CastRoleType ICast.RoleType => CastRoleType.None;

    IMetadata<int>? ICast.Parent => GetTmdbParent();

    ICharacter? ICast.Character => null;

    ICreator? ICast.Creator => GetTmdbPerson();

    #endregion
}
