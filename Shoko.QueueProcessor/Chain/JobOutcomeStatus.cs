namespace Shoko.QueueProcessor.Chain;

public enum JobOutcomeStatus
{
    Succeeded,
    Failed,
    Aborted,
    Skipped,

    /// <summary>
    /// A user cancelled the job: removed it while it waited, or asked it to stop and it did.
    /// </summary>
    Cancelled,
}
