using System.ComponentModel.DataAnnotations.Schema;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Enums;

#nullable enable
namespace Shoko.Server.Models.TMDB;

/// <summary>
/// Cast member for an episode.
/// </summary>
public abstract class TMDB_Cast : ICast
{
    /// <summary>
    /// TMDB Person ID for the cast member.
    /// </summary>
    public int TmdbPersonID { get; set; }

    /// <summary>
    /// TMDB Parent ID for the production job.
    /// </summary>
    [NotMapped]
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

    public virtual TMDB_Person? Person { get; set; }

    public abstract IMetadata<int>? GetTmdbParent();

    string IMetadata<string>.ID => TmdbCreditID;

    DataSourceEnum IMetadata.Source => DataSourceEnum.TMDB;

    IImageMetadata? IWithPortraitImage.PortraitImage => null;

    int? ICast.CreatorID => TmdbPersonID;

    int? ICast.CharacterID => null;

    int ICast.ParentID => TmdbParentID;

    string ICast.Name => CharacterName;

    string? ICast.OriginalName => null;

    string? ICast.Description => null;

    CastRoleType ICast.RoleType => CastRoleType.None;

    IMetadata<int>? ICast.Parent => GetTmdbParent();

    ICharacter? ICast.Character => null;

    ICreator? ICast.Creator => Person;

}
