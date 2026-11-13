namespace Shoko.Abstractions.Core;

/// <summary>
///   The outcome of testing a connection to the database server the database
///   settings name.
/// </summary>
/// <param name="IsSuccess">
///   Whether the server could be reached and signed in to.
/// </param>
/// <param name="ErrorMessage">
///   Why the connection failed, or <c>null</c> when it succeeded.
/// </param>
public record DatabaseConnectionTestResult(bool IsSuccess, string? ErrorMessage = null);
