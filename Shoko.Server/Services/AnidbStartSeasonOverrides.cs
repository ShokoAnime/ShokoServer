using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Shoko.Abstractions.Metadata.Anidb.Models;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Repositories.Cached.AniDB;

namespace Shoko.Server.Services;

/// <summary>
///   Reads and writes the start seasons users set by hand for AniDB anime.
///   The AniDB service goes through it and raises the change events.
/// </summary>
/// <param name="repository">The override repository.</param>
public class AnidbStartSeasonOverrides(AniDB_Anime_StartSeasonOverrideRepository repository)
{
    #region Constants

    /// <summary>
    ///   The earliest year an override may name.
    /// </summary>
    public const int MinimumYear = 1900;

    /// <summary>
    ///   The latest year an override may name.
    /// </summary>
    public const int MaximumYear = 9999;

    #endregion

    #region Fields

    // Keeps two writers from adding a row for the same anime at once.
    private readonly Lock _writeLock = new();

    #endregion

    #region Reading

    /// <summary>
    ///   Gets the override of an AniDB anime.
    /// </summary>
    /// <param name="anidbAnimeID">The AniDB anime ID.</param>
    /// <returns>The override, or <c>null</c> when none is set.</returns>
    public AnidbStartSeasonOverride? Get(int anidbAnimeID)
        => repository.GetByAnimeID(anidbAnimeID)?.ToAbstraction();

    /// <summary>
    ///   Lists every override.
    /// </summary>
    /// <returns>The overrides, by ascending AniDB anime ID.</returns>
    public IReadOnlyList<AnidbStartSeasonOverride> GetAll()
        => [.. repository.GetAll().OrderBy(row => row.AnimeID).Select(row => row.ToAbstraction())];

    #endregion

    #region Writing

    /// <summary>
    ///   Sets the override of an AniDB anime, unless it is set to the same
    ///   season already.
    /// </summary>
    /// <param name="anidbAnimeID">The AniDB anime ID.</param>
    /// <param name="year">The year.</param>
    /// <param name="season">The season.</param>
    /// <param name="userID">The local ID of the user setting it, or <c>null</c> for the system.</param>
    /// <param name="now">The current time, in UTC.</param>
    /// <exception cref="ArgumentOutOfRangeException">An argument is out of range (see <see cref="Validate"/>).</exception>
    /// <returns>
    ///   The override before and after; the two are equal when nothing
    ///   changed.
    /// </returns>
    public (AnidbStartSeasonOverride? Previous, AnidbStartSeasonOverride Current) Set(
        int anidbAnimeID,
        int year,
        YearlySeason season,
        int? userID,
        DateTime now
    )
    {
        Validate(anidbAnimeID, year, season);

        lock (_writeLock)
        {
            var row = repository.GetByAnimeID(anidbAnimeID);
            var previous = row?.ToAbstraction();
            if (row is not null && row.Year == year && row.Season == season)
                return (previous, previous!);

            row ??= new() { AnimeID = anidbAnimeID, CreatedAt = now };
            row.Year = year;
            row.Season = season;
            row.UserID = userID;
            row.UpdatedAt = now;
            repository.Save(row);
            return (previous, row.ToAbstraction());
        }
    }

    /// <summary>
    ///   Removes the override of an AniDB anime.
    /// </summary>
    /// <param name="anidbAnimeID">The AniDB anime ID.</param>
    /// <returns>The override removed, or <c>null</c> when none was set.</returns>
    public AnidbStartSeasonOverride? Remove(int anidbAnimeID)
    {
        lock (_writeLock)
        {
            if (repository.GetByAnimeID(anidbAnimeID) is not { } row)
                return null;

            var previous = row.ToAbstraction();
            repository.Delete(row);
            return previous;
        }
    }

    /// <summary>
    ///   Checks an override's values: an AniDB anime ID above <c>0</c>, a
    ///   year from <see cref="MinimumYear"/> to <see cref="MaximumYear"/>,
    ///   and a defined season.
    /// </summary>
    /// <param name="anidbAnimeID">The AniDB anime ID.</param>
    /// <param name="year">The year.</param>
    /// <param name="season">The season.</param>
    /// <exception cref="ArgumentOutOfRangeException">An argument is out of range.</exception>
    public static void Validate(int anidbAnimeID, int year, YearlySeason season)
    {
        if (anidbAnimeID <= 0)
            throw new ArgumentOutOfRangeException(nameof(anidbAnimeID), anidbAnimeID, "The AniDB anime ID must be above 0.");

        if (year is < MinimumYear or > MaximumYear)
            throw new ArgumentOutOfRangeException(nameof(year), year, $"The year must be from {MinimumYear} to {MaximumYear}.");

        if (!Enum.IsDefined(season))
            throw new ArgumentOutOfRangeException(nameof(season), season, "The season must be Winter, Spring, Summer or Fall.");
    }

    #endregion
}
