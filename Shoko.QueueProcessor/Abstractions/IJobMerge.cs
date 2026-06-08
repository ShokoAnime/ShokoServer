namespace Shoko.QueueProcessor.Abstractions;

/// <summary>
/// Opt-in interface for job types that support parameter merging on queue collision.
/// When a new enqueue collides with a waiting (non-executing) job of the same key,
/// the engine calls <see cref="TryMerge"/> to allow the existing job to absorb the
/// incoming request's more-aggressive parameters.
/// </summary>
public interface IJobMerge
{
    /// <summary>
    /// Merges parameters from <paramref name="incoming"/> into <see langword="this"/> (the existing
    /// queued job). Called only when a waiting job collides with a new enqueue of the same key.
    /// </summary>
    /// <remarks>
    /// Both instances are created via <c>RuntimeHelpers.GetUninitializedObject</c> and hydrated
    /// from stored JSON — injected services will be null. Implementations must only read and write
    /// data properties.
    /// </remarks>
    /// <param name="incoming">
    /// A freshly-deserialized copy of the incoming job with only data properties populated.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if any property was changed (upgrade occurred, triggers a DB write);
    /// <see langword="false"/> if the existing job already covers the incoming request.
    /// </returns>
    bool TryMerge(IQueueJob incoming);
}
