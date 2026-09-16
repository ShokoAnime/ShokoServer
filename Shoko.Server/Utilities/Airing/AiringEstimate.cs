using System;

namespace Shoko.Server.Utilities.Airing;

/// <summary>
/// One schedule's estimate for one episode. Estimates are computed per read and
/// never stored.
/// </summary>
/// <param name="EpisodeKey">
/// The key of the episode the estimate is for.
/// </param>
/// <param name="AiredAt">
/// The estimated slot, in UTC, or <see langword="null"/> when the schedule is on
/// hiatus from that slot on.
/// </param>
/// <param name="OriginalAiredAt">
/// The slot the episode would have had without the schedule's trailing shift or
/// hiatus, in UTC, or <see langword="null"/> when neither applies.
/// </param>
public sealed record AiringEstimate(string EpisodeKey, DateTime? AiredAt, DateTime? OriginalAiredAt);
