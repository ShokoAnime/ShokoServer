using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Exceptions;

namespace Shoko.Abstractions.Config.Exceptions;

/// <summary>
/// Thrown when a plugin configuration fails validation.
/// </summary>
public class ConfigurationValidationException(string saveOrLoad, ConfigurationInfo configurationInfo, IReadOnlyDictionary<string, IReadOnlyList<string>> validationErrors) : GenericValidationException($"Unable to {saveOrLoad} configuration for \"{configurationInfo.Name}\" due to {validationErrors.Sum(a => a.Value.Count)} validation errors occurring.", validationErrors)
{
    /// <summary>
    /// Information about the configuration that failed validation.
    /// </summary>
    public ConfigurationInfo ConfigurationInfo { get; } = configurationInfo;
}
