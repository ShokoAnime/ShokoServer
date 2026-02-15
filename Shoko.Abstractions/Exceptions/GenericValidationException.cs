using System;
using System.Collections.Generic;

namespace Shoko.Abstractions.Exceptions;

/// <summary>
/// Thrown when something fails validation.
/// </summary>
public class GenericValidationException(string message, IReadOnlyDictionary<string, IReadOnlyList<string>> validationErrors) : Exception(message)
{
    /// <summary>
    /// Validation errors that occurred while saving the configuration, per property path.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> ValidationErrors { get; } = validationErrors;
}
