using System;

namespace Shoko.Abstractions.Config.Exceptions;

/// <summary>
/// Thrown when a configuration action is invalid.
/// </summary>
/// <param name="message">The message</param>
/// <param name="paramName">The parameter name</param>
public class InvalidConfigurationActionException(string message, string paramName) : ArgumentException(message, paramName) { }
