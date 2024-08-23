using System;
using Shoko.Server.Repositories;

#nullable enable
namespace Shoko.Server.Models.TMDB;

/// <summary>
/// Crew member for an episode.
/// </summary>
public class TMDB_Crew
{
    #region Properties

    /// <summary>
    /// TMDB Person ID for the crew member.
    /// </summary>
    public int TmdbPersonID { get; set; }

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

    public TMDB_Person GetTmdbPerson() =>
        RepoFactory.TMDB_Person.GetByTmdbPersonID(TmdbPersonID) ??
            throw new Exception($"Unable to find TMDB Person with the given id. (Person={TmdbPersonID})");

    #endregion
}
