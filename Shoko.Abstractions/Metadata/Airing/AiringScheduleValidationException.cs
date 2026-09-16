using System.Collections.Generic;
using Shoko.Abstractions.Exceptions;

namespace Shoko.Abstractions.Metadata.Airing;

/// <summary>
///   Thrown when one or more episode airings fail validation while writing
///   them. A batch reports every rejected airing at once, keyed by airing key,
///   instead of failing on the first.
/// </summary>
/// <param name="message">
///   What went wrong.
/// </param>
/// <param name="validationErrors">
///   What failed validation, keyed by airing key.
/// </param>
public class AiringScheduleValidationException(string message, IReadOnlyDictionary<string, IReadOnlyList<string>> validationErrors)
    : GenericValidationException(message, validationErrors)
{ }
