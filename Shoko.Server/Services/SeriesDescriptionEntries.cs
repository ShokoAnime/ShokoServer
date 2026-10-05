using Shoko.Abstractions.Metadata;

namespace Shoko.Server.Services;

/// <summary>
///   The entries whose descriptions speak for a whole anime on one source.
/// </summary>
/// <param name="Entry">
///   The best-fitting entry: the film, the whole show, the linked season or
///   the one linked special; <c>null</c> when the source says nothing.
/// </param>
/// <param name="Show">
///   The show to fall back on when <paramref name="Entry"/> is one of its
///   seasons, else <c>null</c>.
/// </param>
/// <param name="ShowAtOnce">
///   Whether the show is asked right after the season, as for a first
///   season, rather than once every source's best entry was asked.
/// </param>
internal readonly record struct SeriesDescriptionEntries(IMetadata? Entry, IMetadata? Show, bool ShowAtOnce);
