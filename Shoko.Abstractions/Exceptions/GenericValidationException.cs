using System;
using System.Collections.Generic;

namespace Shoko.Abstractions.Exceptions;

/// <summary>
/// Thrown when something fails validation.
/// </summary>
public class GenericValidationException(string message, IReadOnlyDictionary<string, IReadOnlyList<string>> validationErrors) : Exception(message)
{
    /// <summary>
    /// What failed validation, keyed by the path of the thing that failed. A
    /// path can carry more than one problem, and the whole set is reported at
    /// once so a caller can fix everything in one pass.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> ValidationErrors { get; } = validationErrors;
}
