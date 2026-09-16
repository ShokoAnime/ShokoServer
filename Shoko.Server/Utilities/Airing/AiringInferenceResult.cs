using System.Collections.Generic;

namespace Shoko.Server.Utilities.Airing;

/// <summary>
/// What a run of the delay inference decided: the airings to store, and the
/// airings to remove. Anything in neither list is left exactly as it was.
/// </summary>
/// <param name="ToSave">
/// The airings to store, in the order they should be written.
/// </param>
/// <param name="ToDelete">
/// The existing airings to remove.
/// </param>
public sealed record AiringInferenceResult(IReadOnlyList<InferredAiring> ToSave, IReadOnlyList<ExistingAiring> ToDelete);
