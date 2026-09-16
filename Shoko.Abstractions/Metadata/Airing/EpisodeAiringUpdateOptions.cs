namespace Shoko.Abstractions.Metadata.Airing;

/// <summary>
///   Options for how the service writes a schedule's airings.
/// </summary>
public sealed class EpisodeAiringUpdateOptions
{
    /// <summary>
    ///   Whether to infer delays while writing the airings. Turn it off for a
    ///   provider that reports delays itself; airings are then stored exactly
    ///   as submitted, and removed airings are deleted. Defaults to
    ///   <c>true</c>.
    /// </summary>
    public bool InferDelays { get; init; } = true;
}
