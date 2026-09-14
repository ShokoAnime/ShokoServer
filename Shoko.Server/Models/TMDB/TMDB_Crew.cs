using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Repositories;
using Shoko.Abstractions.Extensions;

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

    public abstract IMetadata<int>? GetTmdbParent();

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

    #region IMetadata Implementation

    string IMetadata<string>.ID => TmdbCreditID;

    DataSource IMetadata.Source => DataSource.TMDB;

    #endregion

    #region ICrew Implementation

    int ICrew.CreatorID => TmdbPersonID;

    int ICrew.ParentID => TmdbParentID;

    string ICrew.Name => $"{Department}, {Job}";

    CrewRoleType ICrew.RoleType => $"{Department}, {Job}" switch
    {
        // TODO: Add these mappings.
        _ => CrewRoleType.None,
    };

    IMetadata<int>? ICrew.Parent => GetTmdbParent();

    ICreator? ICrew.Creator => GetTmdbPerson();

    #endregion
}
