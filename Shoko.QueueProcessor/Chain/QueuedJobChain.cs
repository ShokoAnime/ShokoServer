using System;

namespace Shoko.QueueProcessor.Chain;

/// <summary>
/// EF Core entity for persisting chain context across job executions and restarts.
/// </summary>
public class QueuedJobChain
{
    public Guid ChainId { get; set; }
    public int Status { get; set; }
    public string? DataJson { get; set; }
    public string? ResultsJson { get; set; }
    public string? OutcomesJson { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
