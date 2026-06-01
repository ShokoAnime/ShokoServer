using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Shoko.QueueProcessor.Chain;

/// <summary>
/// Shared in-memory context for all jobs in a chain. Persisted to <see cref="QueuedJobChain"/>
/// after each job executes so crash recovery can reload it.
/// </summary>
public class JobChainContext
{
    private readonly Dictionary<string, string> _data = new(StringComparer.Ordinal);

    // Results keyed by job ID (Guid string). Type-based lookups scan this list for the last matching type.
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

    public T? GetData<T>(string key)
    {
        if (!_data.TryGetValue(key, out var json)) return default;
        return JsonSerializer.Deserialize<T>(json);
    }

    public void SetData<T>(string key, T? value)
    {
        _data[key] = JsonSerializer.Serialize(value);
        IsDirty = true;
    }

    // ── Typed job results ────────────────────────────────────────────────────────────────────────
    // Keyed by job ID so the same job type can appear multiple times in a chain without collision.
    // Type-based accessors return the most recently stored result for that type.

    public T? GetResult<T>(Guid jobId)
    {
        var entry = _results.FirstOrDefault(r => r.JobId == jobId);
        return entry == null ? default : JsonSerializer.Deserialize<T>(entry.Json);
    }

    public T? GetResult<T>(Type jobType)
    {
        var typeKey = TypeKey(jobType);
        var entry = _results.LastOrDefault(r => r.TypeKey == typeKey);
        return entry == null ? default : JsonSerializer.Deserialize<T>(entry.Json);
    }

    internal void SetResult<T>(Guid jobId, Type jobType, T value)
    {
        var typeKey = TypeKey(jobType);
        var idx = _results.FindIndex(r => r.JobId == jobId);
        var entry = new JobResult { JobId = jobId, TypeKey = typeKey, Json = JsonSerializer.Serialize(value) };
        if (idx >= 0)
            _results[idx] = entry;
        else
            _results.Add(entry);
        IsDirty = true;
    }

    // ── Outcome tracking ─────────────────────────────────────────────────────────────────────────

    public JobOutcome? GetOutcome(Guid jobId) => _outcomes.Find(o => o.JobId == jobId);

    public JobOutcome? GetOutcome(Type jobType)
    {
        var typeKey = TypeKey(jobType);
        return _outcomes.LastOrDefault(o => o.JobType == typeKey);
    }

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

    private static string TypeKey(Type jobType) => jobType.AssemblyQualifiedName ?? jobType.FullName!;

    // Used for serialization of the results list
    private class JobResult
    {
        public Guid JobId { get; set; }
        public string TypeKey { get; set; } = string.Empty;
        public string Json { get; set; } = string.Empty;
    }
}
