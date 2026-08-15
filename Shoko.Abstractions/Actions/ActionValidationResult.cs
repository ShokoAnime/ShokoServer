namespace Shoko.Abstractions.Actions;

/// <summary>
///   The result of a failed <see cref="IExecutableAction.Validate"/> call.
///   Returned by actions to reject an invocation before it is enqueued; the
///   API maps it to a 400 response with <see cref="Reason"/> as the message.
/// </summary>
/// <param name="Reason">
///   The reason the invocation was rejected.
/// </param>
public sealed record ActionValidationResult(string Reason);
