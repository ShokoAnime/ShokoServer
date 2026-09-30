using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Repositories;

namespace Shoko.Server.Models.TMDB;

/// <summary>
/// Crew member for an episode.
/// </summary>
public abstract class TMDB_Crew : ICrew
{
    #region Properties

    /// <summary>
    /// TMDB Person ID for the crew member.
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

    #endregion

    #region Methods

    public TMDB_Person? GetTmdbPerson() =>
        RepoFactory.TMDB_Person.GetByTmdbPersonID(TmdbPersonID);

    public abstract IMetadata? GetTmdbParent();

    /// <summary>
    /// TMDB only lists the original-language crew, so every role is in the
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

    #region ICrew Implementation

    MetadataSource ICrew.Source => MetadataSource.TMDB;

    MetadataGuid ICrew.CreatorID => new(MetadataSource.TMDB, MetadataEntityType.Creator, TmdbPersonID.ToString());

    MetadataGuid ICrew.ParentID => new(MetadataSource.TMDB, ParentType, TmdbParentID.ToString());

    string ICrew.Name => $"{Department}, {Job}";

    CrewRoleType ICrew.RoleType => $"{Department}, {Job}" switch
    {
        // TODO: Add these mappings.
        _ => CrewRoleType.None,
    };

    IMetadata? ICrew.Parent => GetTmdbParent();

    ICreator? ICrew.Creator => GetTmdbPerson();

    #endregion
}
