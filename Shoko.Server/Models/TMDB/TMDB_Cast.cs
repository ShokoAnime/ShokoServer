using System;
using Shoko.Server.Repositories;

#nullable enable
namespace Shoko.Server.Models.TMDB;

/// <summary>
/// Cast member for an episode.
/// </summary>
public class TMDB_Cast
{
    #region Properties

    /// <summary>
    /// TMDB Person ID for the cast member.
    /// </summary>
    public int TmdbPersonID { get; set; }

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

    public TMDB_Person GetTmdbPerson() =>
        RepoFactory.TMDB_Person.GetByTmdbPersonID(TmdbPersonID) ??
            throw new Exception($"Unable to find TMDB Person with the given id. (Person={TmdbPersonID})");

    #endregion
}
