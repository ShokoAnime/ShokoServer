using System;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.DataModels.Shoko;
using Shoko.Plugin.Abstractions.Enums;

namespace Shoko.Plugin.Abstractions.Events;

/// <summary>
/// Event arguments for when a user votes/rates a series.
/// </summary>
public class SeriesVotedEventArgs : EventArgs
{
    /// <summary>
    /// The Shoko series that was voted on.
    /// </summary>
    public IShokoSeries Series { get; }

    /// <summary>
    /// The AniDB anime metadata.
    /// </summary>
    public ISeries AnidbAnime { get; }

    /// <summary>
    /// The vote value (normalized to AniDB's scale).
    /// </summary>
    public decimal VoteValue { get; }

    /// <summary>
    /// The type of vote.
    /// </summary>
    public VoteType VoteType { get; }

    /// <summary>
    /// The user who submitted the vote (null if from v1).
    /// </summary>
    public IShokoUser? User { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SeriesVotedEventArgs"/> class.
    /// </summary>
    public SeriesVotedEventArgs(IShokoSeries series, ISeries anidbAnime, decimal voteValue, VoteType voteType, IShokoUser? user)
    {
        Series = series;
        AnidbAnime = anidbAnime;
        VoteValue = voteValue;
        VoteType = voteType;
        User = user;
    }
}
