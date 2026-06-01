using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Shoko.QueueProcessor.Chain;

/// <summary>
/// Shared in-memory context for all jobs in a chain. Persisted to <see cref="QueuedJobChain"/>
/// after each job executes so crash recovery can reload it.
/// </summary>
public class JobChainContext
{
    private readonly Dictionary<string, string> _data = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _results = new(StringComparer.Ordinal);
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

    // ── Typed job results (set automatically by BaseJob<T>.Execute) ──────────────────────────────

    public T? GetResult<T>(Type jobType)
    {
        if (!_results.TryGetValue(jobType.AssemblyQualifiedName ?? jobType.FullName!, out var json)) return default;
        return JsonSerializer.Deserialize<T>(json);
    }

    public void SetResult<T>(Type jobType, T value)
    {
        _results[jobType.AssemblyQualifiedName ?? jobType.FullName!] = JsonSerializer.Serialize(value);
        IsDirty = true;
    }

    // ── Outcome tracking ─────────────────────────────────────────────────────────────────────────

    public JobOutcome? GetOutcome(Type jobType)
    {
        var name = jobType.AssemblyQualifiedName ?? jobType.FullName!;
        return _outcomes.Find(o => o.JobType == name);
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
            var results = JsonSerializer.Deserialize<Dictionary<string, string>>(resultsJson);
            if (results != null)
                foreach (var (k, v) in results) ctx._results[k] = v;
        }
        if (!string.IsNullOrEmpty(outcomesJson))
        {
            var outcomes = JsonSerializer.Deserialize<List<JobOutcome>>(outcomesJson);
            if (outcomes != null) ctx._outcomes.AddRange(outcomes);
        }
        return ctx;
    }
}
