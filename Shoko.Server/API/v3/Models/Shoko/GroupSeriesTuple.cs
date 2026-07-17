namespace Shoko.Server.API.v3.Models.Shoko;

/// <summary>
///   A lightweight (GroupID, SeriesID) tuple returned by the TupleIDs
///   endpoints for client-side filtering and computation.
/// </summary>
public sealed class GroupSeriesTuple
{
    /// <summary>
    ///   The group ID.
    /// </summary>
    public required int GroupID { get; init; }

    /// <summary>
    ///   The series ID.
    /// </summary>
    public required int SeriesID { get; init; }
}
