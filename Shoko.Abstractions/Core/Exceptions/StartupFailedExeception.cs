using System;

namespace Shoko.Abstractions.Core.Exceptions;

/// <summary>
///   Startup failed exception.
/// </summary>
/// <param name="message">Message.</param>
/// <param name="innerException">Inner exception.</param>
public class StartupFailedException(string? message = null, Exception? innerException = null) : Exception(message ?? innerException?.Message);
