using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Shoko.QueueProcessor.Chain;

/// <summary>
/// Shared in-memory context for all jobs in a chain, persisted to <see cref="QueuedJobChain"/>
/// after each job executes so crash recovery can reload it.
/// </summary>
/// <remarks>
/// Three independent stores live here:
/// <list type="bullet">
///   <item>
///     <term>Data</term>
///     <description>
///       A mutable string-keyed bag (<see cref="GetData{T}"/> / <see cref="SetData{T}"/>).
///       Any job in the chain can read or write any key; last write wins. Use it to pass
///       arbitrary forward state (e.g. an ID produced by an earlier job that later jobs need).
///     </description>
///   </item>
///   <item>
///     <term>Results</term>
///     <description>
///       A chronological log of per-job typed results. Each time a job calls <c>SetResult</c>
///       a new entry is appended, so retried jobs (same ID) accumulate one entry per attempt.
///       <see cref="GetResult{T}(string)"/> returns the most recent result for a job key.
///       <see cref="GetResult{T}(Guid)"/> returns all attempts in chronological order
///       (index 0 = first attempt, last index = most recent attempt).
///     </description>
///   </item>
///   <item>
///     <term>Outcomes</term>
///     <description>
///       An append-only audit log of <see cref="JobOutcome"/> records, one per job completion
///       (success, failure, abort, or skip). Ordered by completion time.
///       <see cref="GetOutcome(Guid)"/> returns the first recorded outcome for a job ID (its
///       initial attempt). <see cref="GetOutcome(string)"/> returns the most recent outcome
///       for a job key (its last attempt).
///     </description>
///   </item>
/// </list>
/// </remarks>
public class JobChainContext
{
    private readonly Dictionary<string, string> _data = new(StringComparer.Ordinal);

    // Chronological log of results. Multiple entries can share a JobId (one per retry attempt).
    // Key-based lookup returns the last entry for that key; ID-based lookup returns all entries.
    private readonly List<JobResult> _results = [];
    private readonly List<JobOutcome> _outcomes = [];

    internal bool IsDirty { get; private set; }

    public Guid ChainId { get; }
    public ChainStatus Status { get; private set; }

    public JobChainContext(Guid chainId, ChainStatus status = ChainStatus.Active)
    {
        ChainId = chainId;
        Status = status;
    }

    // ── Shared data store ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Reads a shared value from the chain data bag. Returns <c>default</c> if the key does not exist.
    /// </summary>
    public T? GetData<T>(string key)
    {
        if (!_data.TryGetValue(key, out var json)) return default;
        return JsonSerializer.Deserialize<T>(json);
    }

    /// <summary>
    /// Writes a shared value to the chain data bag. Overwrites any existing value for <paramref name="key"/>.
    /// </summary>
    public void SetData<T>(string key, T? value)
    {
        _data[key] = JsonSerializer.Serialize(value);
        IsDirty = true;
    }

    // ── Typed job results ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns all results stored by the job identified by <paramref name="jobId"/>, in the order
    /// they were recorded. Index 0 is the first attempt; the last index is the most recent attempt.
    /// Returns an empty list if the job has not stored any result.
    /// </summary>
    public IReadOnlyList<T> GetResult<T>(Guid jobId) =>
        _results
            .Where(r => r.JobId == jobId)
            .Select(r => JsonSerializer.Deserialize<T>(r.Json)!)
            .ToList();

    /// <summary>
    /// Returns the most recent result stored by any job whose key matches <paramref name="jobKey"/>.
    /// Returns <c>default</c> if no matching result exists.
    /// </summary>
    public T? GetResult<T>(string jobKey)
    {
        var entry = _results.LastOrDefault(r => r.JobKey == jobKey);
        return entry == null ? default : JsonSerializer.Deserialize<T>(entry.Json);
    }

    /// <summary>
    /// Appends a result entry for the given job. Called once per attempt, so retried jobs
    /// accumulate multiple entries under the same <paramref name="jobId"/>.
    /// </summary>
    internal void SetResult<T>(Guid jobId, string jobKey, T value)
    {
        _results.Add(new JobResult { JobId = jobId, JobKey = jobKey, Json = JsonSerializer.Serialize(value) });
        IsDirty = true;
    }

    // ── Outcome tracking ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the first recorded outcome for <paramref name="jobId"/> (its initial attempt),
    /// or <c>null</c> if no outcome has been recorded for that job.
    /// </summary>
    public JobOutcome? GetOutcome(Guid jobId) => _outcomes.Find(o => o.JobId == jobId);

    /// <summary>
    /// Returns the most recent outcome for the job identified by <paramref name="jobKey"/>,
    /// or <c>null</c> if no outcome has been recorded for that key.
    /// </summary>
    public JobOutcome? GetOutcome(string jobKey) => _outcomes.LastOrDefault(o => o.JobKey == jobKey);

    /// <summary>Returns all recorded outcomes in the order they were appended.</summary>
    public IReadOnlyList<JobOutcome> GetAllOutcomes() => _outcomes;

    // ── Internal mutation (Worker + Orchestrator) ────────────────────────────────────────────────

    internal void AddOutcome(JobOutcome outcome)
    {
        _outcomes.Add(outcome);
        IsDirty = true;
    }

    internal void SetStatus(ChainStatus status)
    {
        Status = status;
        IsDirty = true;
    }

    internal void MarkClean() => IsDirty = false;

    // ── Serialization helpers (used by JobChainContextRepository) ────────────────────────────────

    internal string SerializeData() => JsonSerializer.Serialize(_data);
    internal string SerializeResults() => JsonSerializer.Serialize(_results);
    internal string SerializeOutcomes() => JsonSerializer.Serialize(_outcomes);

    internal static JobChainContext Deserialize(
        Guid chainId,
        ChainStatus status,
        string? dataJson,
        string? resultsJson,
        string? outcomesJson)
    {
        var ctx = new JobChainContext(chainId, status);
        if (!string.IsNullOrEmpty(dataJson))
        {
            var data = JsonSerializer.Deserialize<Dictionary<string, string>>(dataJson);
            if (data != null)
                foreach (var (k, v) in data) ctx._data[k] = v;
        }
        if (!string.IsNullOrEmpty(resultsJson))
        {
            var results = JsonSerializer.Deserialize<List<JobResult>>(resultsJson);
            if (results != null) ctx._results.AddRange(results);
        }
        if (!string.IsNullOrEmpty(outcomesJson))
        {
            var outcomes = JsonSerializer.Deserialize<List<JobOutcome>>(outcomesJson);
            if (outcomes != null) ctx._outcomes.AddRange(outcomes);
        }
        return ctx;
    }

    private class JobResult
    {
        public Guid JobId { get; set; }
        public string JobKey { get; set; } = string.Empty;
        public string Json { get; set; } = string.Empty;
    }
}
