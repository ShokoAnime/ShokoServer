
using System;
using System.Collections.Generic;
using System.Linq;

namespace Shoko.Plugin.Abstractions.Config.Exceptions;

/// <summary>
/// Thrown when a plugin configuration fails validation.
/// </summary>
public class ConfigurationValidationException(string saveOrLoad, ConfigurationInfo configurationInfo, IReadOnlyDictionary<string, IReadOnlyList<string>> validationErrors) : Exception($"Unable to {saveOrLoad} configuration for \"{configurationInfo.Name}\" due to {validationErrors.Sum(a => a.Value.Count)} validation errors occurring.")
{
    /// <summary>
    /// Information about the configuration that failed validation.
    /// </summary>
    public ConfigurationInfo ConfigurationInfo { get; } = configurationInfo;

    /// <summary>
    /// Validation errors that occurred while saving the configuration, per property path.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> ValidationErrors { get; } = validationErrors;
}
