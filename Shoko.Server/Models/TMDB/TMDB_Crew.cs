using System.ComponentModel.DataAnnotations.Schema;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Enums;

#nullable enable
namespace Shoko.Server.Models.TMDB;

/// <summary>
/// Crew member for an episode.
/// </summary>
public abstract class TMDB_Crew : ICrew
{
    /// <summary>
    /// TMDB Person ID for the crew member.
    /// </summary>
    public int TmdbPersonID { get; set; }

    /// <summary>
    /// TMDB Parent ID for the production job.
    /// </summary>
    [NotMapped]
    public abstract int TmdbParentID { get; }

    /// <summary>
    /// TMDB Credit ID for the production job.
    /// </summary>
    public string TmdbCreditID { get; set; } = string.Empty;

    /// <summary>
    /// The job title.
    /// </summary>
    public string Job { get; set; } = string.Empty;

    /// <summary>
    /// The crew department.
    /// </summary>
    public string Department { get; set; } = string.Empty;

    public virtual TMDB_Person? Person { get; set; }

    public abstract IMetadata<int>? GetTmdbParent();

    string IMetadata<string>.ID => TmdbCreditID;

    DataSourceEnum IMetadata.Source => DataSourceEnum.TMDB;

    int ICrew.CreatorID => TmdbPersonID;

    int ICrew.ParentID => TmdbParentID;

    string ICrew.Name => $"{Department}, {Job}";

    CrewRoleType ICrew.RoleType => $"{Department}, {Job}" switch
    {
        // TODO: Add these mappings.
        _ => CrewRoleType.None,
    };

    IMetadata<int>? ICrew.Parent => GetTmdbParent();

    ICreator? ICrew.Creator => Person;
}
