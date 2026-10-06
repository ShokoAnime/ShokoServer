using Shoko.Abstractions.Metadata;

#nullable enable
namespace Shoko.Server.Services.Airing;

/// <summary>
/// What a read gathers the airings of: an episode, a place on a line no
/// episode is listed at yet, or both, under the key the read merges them by.
/// </summary>
/// <param name="Episode">The episode, or <c>null</c> for a place alone.</param>
/// <param name="Key">The key the gathered airings count as.</param>
internal readonly record struct AiringReadTarget(IEpisode? Episode, AiringEpisodeKey Key);
