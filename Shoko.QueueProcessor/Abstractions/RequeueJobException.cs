namespace Shoko.QueueProcessor.Abstractions;

/// <summary>
/// Thrown by a job to signal that it should be re-queued immediately without incrementing
/// the retry count. The job simply returns to the waiting queue at its original priority
/// and will be dispatched again when eligible.
/// <para>
/// Use this for transient conditions managed by acquisition filters — e.g., AniDB bans,
/// login failures — where the filter will naturally block re-execution until the condition
/// clears, so exponential backoff is inappropriate.
/// </para>
/// <example>
/// <code>
/// // In BaseJob:
/// catch (AniDBBannedException)   { throw new RequeueJobException(); }
/// catch (NotLoggedInException)   { throw new RequeueJobException(); }
/// catch (LoginFailedException)   { throw new RequeueJobException(); }
/// </code>
/// </example>
/// </summary>
public sealed class RequeueJobException : System.Exception
{
    public RequeueJobException() : base("Job requested re-queue without retry increment.") { }
}
