using System;
using Shoko.QueueProcessor.Chain;
using Xunit;

namespace Shoko.QueueProcessor.Tests;

/// <summary>
/// Unit tests for <see cref="JobChainContext"/> covering the three data stores
/// (shared data bag, per-job results, outcome log), dirty tracking, and serialization round-trips.
/// </summary>
public class JobChainContextTests
{
    private static JobOutcome MakeOutcome(Guid jobId, string jobKey, JobOutcomeStatus status = JobOutcomeStatus.Succeeded) =>
        new()
        {
            JobId = jobId,
            JobType = "TestJob, TestAssembly",
            JobKey = jobKey,
            Status = status,
            CompletedAt = DateTimeOffset.UtcNow,
        };

    // ── Initial state ─────────────────────────────────────────────────────────

    [Fact]
    public void NewContext_IsNotDirty()
    {
        var ctx = new JobChainContext(Guid.NewGuid());
        Assert.False(ctx.IsDirty);
    }

    [Fact]
    public void NewContext_StatusIsActive()
    {
        var ctx = new JobChainContext(Guid.NewGuid());
        Assert.Equal(ChainStatus.Active, ctx.Status);
    }

    [Fact]
    public void NewContext_Constructor_SetsChainId()
    {
        var id = Guid.NewGuid();
        var ctx = new JobChainContext(id);
        Assert.Equal(id, ctx.ChainId);
    }

    // ── Shared data bag ───────────────────────────────────────────────────────

    [Fact]
    public void SetData_GetData_RoundTrip_String()
    {
        var ctx = new JobChainContext(Guid.NewGuid());
        ctx.SetData("greeting", "hello world");
        Assert.Equal("hello world", ctx.GetData<string>("greeting"));
    }

    [Fact]
    public void SetData_GetData_RoundTrip_Int()
    {
        var ctx = new JobChainContext(Guid.NewGuid());
        ctx.SetData("count", 42);
        Assert.Equal(42, ctx.GetData<int>("count"));
    }

    [Fact]
    public void SetData_ExistingKey_Overwrites()
    {
        var ctx = new JobChainContext(Guid.NewGuid());
        ctx.SetData("key", "first");
        ctx.SetData("key", "second");
        Assert.Equal("second", ctx.GetData<string>("key"));
    }

    [Fact]
    public void SetData_NullValue_Persists()
    {
        var ctx = new JobChainContext(Guid.NewGuid());
        ctx.SetData("key", "original");
        ctx.SetData<string?>("key", null);
        Assert.Null(ctx.GetData<string>("key"));
    }

    [Fact]
    public void GetData_MissingKey_ReturnsDefault()
    {
        var ctx = new JobChainContext(Guid.NewGuid());
        Assert.Null(ctx.GetData<string>("missing"));
        Assert.Equal(0, ctx.GetData<int>("missing"));
    }

    [Fact]
    public void SetData_MarksDirty()
    {
        var ctx = new JobChainContext(Guid.NewGuid());
        ctx.SetData("k", "v");
        Assert.True(ctx.IsDirty);
    }

    // ── Results: key-based ────────────────────────────────────────────────────

    [Fact]
    public void GetResult_ByKey_MissingKey_ReturnsDefault()
    {
        var ctx = new JobChainContext(Guid.NewGuid());
        Assert.Null(ctx.GetResult<string>("missing-key"));
        Assert.Equal(0, ctx.GetResult<int>("missing-key"));
    }

    [Fact]
    public void GetResult_ByKey_SingleResult_ReturnsIt()
    {
        var ctx = new JobChainContext(Guid.NewGuid());
        var jobId = Guid.NewGuid();
        ctx.SetResult(jobId, "job:1", "result-value");
        Assert.Equal("result-value", ctx.GetResult<string>("job:1"));
    }

    [Fact]
    public void GetResult_ByKey_MultipleJobsSameKey_ReturnsLast()
    {
        var ctx = new JobChainContext(Guid.NewGuid());
        // Two attempts of the same job (same key, same ID but two SetResult calls — retry scenario)
        var jobId = Guid.NewGuid();
        ctx.SetResult(jobId, "job:1", "attempt-1");
        ctx.SetResult(jobId, "job:1", "attempt-2");
        Assert.Equal("attempt-2", ctx.GetResult<string>("job:1"));
    }

    [Fact]
    public void GetResult_ByKey_DifferentKeys_ReturnCorrectValue()
    {
        var ctx = new JobChainContext(Guid.NewGuid());
        ctx.SetResult(Guid.NewGuid(), "job:A", "result-A");
        ctx.SetResult(Guid.NewGuid(), "job:B", "result-B");
        Assert.Equal("result-A", ctx.GetResult<string>("job:A"));
        Assert.Equal("result-B", ctx.GetResult<string>("job:B"));
    }

    // ── Results: ID-based (all attempts) ──────────────────────────────────────

    [Fact]
    public void GetResult_ById_NoResults_ReturnsEmptyList()
    {
        var ctx = new JobChainContext(Guid.NewGuid());
        var result = ctx.GetResult<string>(Guid.NewGuid());
        Assert.Empty(result);
    }

    [Fact]
    public void GetResult_ById_SingleAttempt_ReturnsSingleElement()
    {
        var ctx = new JobChainContext(Guid.NewGuid());
        var jobId = Guid.NewGuid();
        ctx.SetResult(jobId, "job:1", "the-result");
        var results = ctx.GetResult<string>(jobId);
        Assert.Single(results);
        Assert.Equal("the-result", results[0]);
    }

    [Fact]
    public void GetResult_ById_MultipleAttempts_ReturnsAllInChronologicalOrder()
    {
        var ctx = new JobChainContext(Guid.NewGuid());
        var jobId = Guid.NewGuid();
        ctx.SetResult(jobId, "job:1", "attempt-1");
        ctx.SetResult(jobId, "job:1", "attempt-2");
        ctx.SetResult(jobId, "job:1", "attempt-3");

        var results = ctx.GetResult<string>(jobId);
        Assert.Equal(3, results.Count);
        Assert.Equal("attempt-1", results[0]);
        Assert.Equal("attempt-2", results[1]);
        Assert.Equal("attempt-3", results[2]);
    }

    [Fact]
    public void GetResult_ById_OnlyReturnsEntriesForThatId()
    {
        var ctx = new JobChainContext(Guid.NewGuid());
        var jobA = Guid.NewGuid();
        var jobB = Guid.NewGuid();
        ctx.SetResult(jobA, "job:A", "result-A");
        ctx.SetResult(jobB, "job:B", "result-B");

        var resultsA = ctx.GetResult<string>(jobA);
        Assert.Single(resultsA);
        Assert.Equal("result-A", resultsA[0]);

        var resultsB = ctx.GetResult<string>(jobB);
        Assert.Single(resultsB);
        Assert.Equal("result-B", resultsB[0]);
    }

    [Fact]
    public void SetResult_MarksDirty()
    {
        var ctx = new JobChainContext(Guid.NewGuid());
        ctx.SetResult(Guid.NewGuid(), "job:1", "value");
        Assert.True(ctx.IsDirty);
    }

    // ── Outcomes ──────────────────────────────────────────────────────────────

    [Fact]
    public void GetOutcome_ById_NoOutcome_ReturnsNull()
    {
        var ctx = new JobChainContext(Guid.NewGuid());
        Assert.Null(ctx.GetOutcome(Guid.NewGuid()));
    }

    [Fact]
    public void GetOutcome_ByKey_NoOutcome_ReturnsNull()
    {
        var ctx = new JobChainContext(Guid.NewGuid());
        Assert.Null(ctx.GetOutcome("missing-key"));
    }

    [Fact]
    public void GetOutcome_ById_ReturnsFirstAttempt()
    {
        var ctx = new JobChainContext(Guid.NewGuid());
        var jobId = Guid.NewGuid();
        var first = MakeOutcome(jobId, "job:1", JobOutcomeStatus.Failed);
        var second = MakeOutcome(jobId, "job:1", JobOutcomeStatus.Succeeded);
        ctx.AddOutcome(first);
        ctx.AddOutcome(second);

        var found = ctx.GetOutcome(jobId);
        Assert.NotNull(found);
        Assert.Equal(JobOutcomeStatus.Failed, found!.Status);
    }

    [Fact]
    public void GetOutcome_ByKey_ReturnsMostRecent()
    {
        var ctx = new JobChainContext(Guid.NewGuid());
        var jobId = Guid.NewGuid();
        ctx.AddOutcome(MakeOutcome(jobId, "job:1", JobOutcomeStatus.Failed));
        ctx.AddOutcome(MakeOutcome(jobId, "job:1", JobOutcomeStatus.Succeeded));

        var found = ctx.GetOutcome("job:1");
        Assert.NotNull(found);
        Assert.Equal(JobOutcomeStatus.Succeeded, found!.Status);
    }

    [Fact]
    public void GetAllOutcomes_ReturnsAllInOrder()
    {
        var ctx = new JobChainContext(Guid.NewGuid());
        var o1 = MakeOutcome(Guid.NewGuid(), "job:A", JobOutcomeStatus.Succeeded);
        var o2 = MakeOutcome(Guid.NewGuid(), "job:B", JobOutcomeStatus.Failed);
        var o3 = MakeOutcome(Guid.NewGuid(), "job:C", JobOutcomeStatus.Skipped);
        ctx.AddOutcome(o1);
        ctx.AddOutcome(o2);
        ctx.AddOutcome(o3);

        var all = ctx.GetAllOutcomes();
        Assert.Equal(3, all.Count);
        Assert.Equal("job:A", all[0].JobKey);
        Assert.Equal("job:B", all[1].JobKey);
        Assert.Equal("job:C", all[2].JobKey);
    }

    [Fact]
    public void AddOutcome_MarksDirty()
    {
        var ctx = new JobChainContext(Guid.NewGuid());
        ctx.AddOutcome(MakeOutcome(Guid.NewGuid(), "job:1"));
        Assert.True(ctx.IsDirty);
    }

    // ── Status ────────────────────────────────────────────────────────────────

    [Fact]
    public void SetStatus_ChangesStatus()
    {
        var ctx = new JobChainContext(Guid.NewGuid());
        ctx.SetStatus(ChainStatus.Aborted);
        Assert.Equal(ChainStatus.Aborted, ctx.Status);
    }

    [Fact]
    public void SetStatus_MarksDirty()
    {
        var ctx = new JobChainContext(Guid.NewGuid());
        ctx.SetStatus(ChainStatus.Completed);
        Assert.True(ctx.IsDirty);
    }

    // ── Dirty / clean tracking ────────────────────────────────────────────────

    [Fact]
    public void MarkClean_ResetsDirtyFlag()
    {
        var ctx = new JobChainContext(Guid.NewGuid());
        ctx.SetData("k", "v");
        Assert.True(ctx.IsDirty);
        ctx.MarkClean();
        Assert.False(ctx.IsDirty);
    }

    [Fact]
    public void MarkClean_ThenMutate_DirtyAgain()
    {
        var ctx = new JobChainContext(Guid.NewGuid());
        ctx.SetData("k", "v");
        ctx.MarkClean();
        ctx.SetResult(Guid.NewGuid(), "job:1", "r");
        Assert.True(ctx.IsDirty);
    }

    // ── Serialization round-trip ──────────────────────────────────────────────

    [Fact]
    public void Deserialize_RoundTrips_DataStore()
    {
        var chainId = Guid.NewGuid();
        var original = new JobChainContext(chainId);
        original.SetData("answer", 42);
        original.SetData("label", "hello");

        var restored = JobChainContext.Deserialize(
            chainId, ChainStatus.Active,
            original.SerializeData(), null, null);

        Assert.Equal(42, restored.GetData<int>("answer"));
        Assert.Equal("hello", restored.GetData<string>("label"));
    }

    [Fact]
    public void Deserialize_RoundTrips_Results_IncludingMultipleAttempts()
    {
        var chainId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        var original = new JobChainContext(chainId);
        original.SetResult(jobId, "job:1", "attempt-1");
        original.SetResult(jobId, "job:1", "attempt-2");

        var restored = JobChainContext.Deserialize(
            chainId, ChainStatus.Active,
            null, original.SerializeResults(), null);

        var results = restored.GetResult<string>(jobId);
        Assert.Equal(2, results.Count);
        Assert.Equal("attempt-1", results[0]);
        Assert.Equal("attempt-2", results[1]);

        Assert.Equal("attempt-2", restored.GetResult<string>("job:1"));
    }

    [Fact]
    public void Deserialize_RoundTrips_Outcomes()
    {
        var chainId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        var original = new JobChainContext(chainId);
        original.AddOutcome(MakeOutcome(jobId, "job:1", JobOutcomeStatus.Succeeded));

        var restored = JobChainContext.Deserialize(
            chainId, ChainStatus.Active,
            null, null, original.SerializeOutcomes());

        var outcome = restored.GetOutcome(jobId);
        Assert.NotNull(outcome);
        Assert.Equal(jobId, outcome!.JobId);
        Assert.Equal(JobOutcomeStatus.Succeeded, outcome.Status);
    }

    [Fact]
    public void Deserialize_NullJsonFields_ProducesEmptyContext()
    {
        var chainId = Guid.NewGuid();
        var ctx = JobChainContext.Deserialize(chainId, ChainStatus.Active, null, null, null);

        Assert.Equal(chainId, ctx.ChainId);
        Assert.Equal(ChainStatus.Active, ctx.Status);
        Assert.Null(ctx.GetData<string>("any"));
        Assert.Empty(ctx.GetResult<string>(Guid.NewGuid()));
        Assert.Empty(ctx.GetAllOutcomes());
    }

    [Fact]
    public void Deserialize_RestoresStatus()
    {
        var chainId = Guid.NewGuid();
        var ctx = JobChainContext.Deserialize(chainId, ChainStatus.Aborted, null, null, null);
        Assert.Equal(ChainStatus.Aborted, ctx.Status);
    }
}
