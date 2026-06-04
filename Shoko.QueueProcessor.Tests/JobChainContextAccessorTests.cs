using System;
using System.Collections.Generic;
using Shoko.QueueProcessor.Chain;
using Xunit;

namespace Shoko.QueueProcessor.Tests;

/// <summary>
/// Unit tests for <see cref="JobChainContextAccessor"/> covering delegation to
/// <see cref="JobChainContext"/>, job-key tagging on <c>SetResult</c>, and the
/// no-context (uninitialized) guard paths.
/// </summary>
public class JobChainContextAccessorTests
{
    private static JobChainContextAccessor MakeAccessor(out JobChainContext ctx)
    {
        var accessor = new JobChainContextAccessor();
        ctx = new JobChainContext(Guid.NewGuid());
        accessor.Initialize(ctx);
        return accessor;
    }

    // ── No-context guard paths ────────────────────────────────────────────────

    [Fact]
    public void GetCurrentContext_BeforeInitialize_ReturnsNull()
    {
        var accessor = new JobChainContextAccessor();
        Assert.Null(accessor.GetCurrentContext());
    }

    [Fact]
    public void GetResult_ByKey_BeforeInitialize_ReturnsDefault()
    {
        var accessor = new JobChainContextAccessor();
        Assert.Null(accessor.GetResult<string>("any-key"));
        Assert.Equal(0, accessor.GetResult<int>("any-key"));
    }

    [Fact]
    public void GetResult_ById_BeforeInitialize_ReturnsEmptyList()
    {
        var accessor = new JobChainContextAccessor();
        Assert.Empty(accessor.GetResult<string>(Guid.NewGuid()));
    }

    [Fact]
    public void GetData_BeforeInitialize_ReturnsDefault()
    {
        var accessor = new JobChainContextAccessor();
        Assert.Null(accessor.GetData<string>("any"));
    }

    [Fact]
    public void SetResult_BeforeInitialize_IsNoOp()
    {
        var accessor = new JobChainContextAccessor();
        accessor.SetCurrentJob(Guid.NewGuid(), "job:1");
        var ex = Record.Exception(() => accessor.SetResult("value"));
        Assert.Null(ex);
    }

    [Fact]
    public void SetData_BeforeInitialize_IsNoOp()
    {
        var accessor = new JobChainContextAccessor();
        var ex = Record.Exception(() => accessor.SetData("k", "v"));
        Assert.Null(ex);
    }

    // ── Initialize ────────────────────────────────────────────────────────────

    [Fact]
    public void Initialize_ExposesContextViaGetCurrentContext()
    {
        var accessor = MakeAccessor(out var ctx);
        Assert.Same(ctx, accessor.GetCurrentContext());
    }

    // ── SetResult / GetResult delegation ──────────────────────────────────────

    [Fact]
    public void SetResult_WithCurrentJob_StoresResultUnderJobKey()
    {
        var accessor = MakeAccessor(out _);
        var jobId = Guid.NewGuid();
        accessor.SetCurrentJob(jobId, "job:process");

        accessor.SetResult("the-result");

        Assert.Equal("the-result", accessor.GetResult<string>("job:process"));
    }

    [Fact]
    public void SetResult_BeforeSetCurrentJob_IsNoOp()
    {
        // Initialize called but SetCurrentJob not called yet — _currentJobId is Guid.Empty
        var accessor = MakeAccessor(out var ctx);
        accessor.SetResult("orphan-result");

        // Nothing should be in the results store
        Assert.Empty(ctx.GetAllOutcomes());
        Assert.Null(accessor.GetResult<string>("any-key"));
    }

    [Fact]
    public void GetResult_ByKey_AfterSetResult_ReturnsMostRecent()
    {
        var accessor = MakeAccessor(out _);
        var jobId = Guid.NewGuid();
        accessor.SetCurrentJob(jobId, "job:1");

        accessor.SetResult("first");
        accessor.SetResult("second");

        Assert.Equal("second", accessor.GetResult<string>("job:1"));
    }

    [Fact]
    public void GetResult_ById_AfterMultipleSetResults_ReturnsAllInOrder()
    {
        var accessor = MakeAccessor(out _);
        var jobId = Guid.NewGuid();
        accessor.SetCurrentJob(jobId, "job:1");

        accessor.SetResult("attempt-1");
        accessor.SetResult("attempt-2");

        var results = accessor.GetResult<string>(jobId);
        Assert.Equal(2, results.Count);
        Assert.Equal("attempt-1", results[0]);
        Assert.Equal("attempt-2", results[1]);
    }

    [Fact]
    public void SetCurrentJob_ChangingJobs_ResultsTaggedByCorrectKey()
    {
        var accessor = MakeAccessor(out _);

        var jobAId = Guid.NewGuid();
        accessor.SetCurrentJob(jobAId, "job:A");
        accessor.SetResult(100);

        var jobBId = Guid.NewGuid();
        accessor.SetCurrentJob(jobBId, "job:B");
        accessor.SetResult(200);

        Assert.Equal(100, accessor.GetResult<int>("job:A"));
        Assert.Equal(200, accessor.GetResult<int>("job:B"));

        var aResults = accessor.GetResult<int>(jobAId);
        Assert.Single(aResults);
        Assert.Equal(100, aResults[0]);

        var bResults = accessor.GetResult<int>(jobBId);
        Assert.Single(bResults);
        Assert.Equal(200, bResults[0]);
    }

    // ── Data delegation ───────────────────────────────────────────────────────

    [Fact]
    public void SetData_GetData_RoundTrip()
    {
        var accessor = MakeAccessor(out _);
        accessor.SetData("key", "value");
        Assert.Equal("value", accessor.GetData<string>("key"));
    }

    [Fact]
    public void SetData_VisibleOnUnderlyingContext()
    {
        var accessor = MakeAccessor(out var ctx);
        accessor.SetData("shared", 42);
        Assert.Equal(42, ctx.GetData<int>("shared"));
    }

    [Fact]
    public void GetData_ReadFromUnderlyingContext()
    {
        var accessor = MakeAccessor(out var ctx);
        ctx.SetData("upstream", "written-directly");
        Assert.Equal("written-directly", accessor.GetData<string>("upstream"));
    }

    // ── Result types ──────────────────────────────────────────────────────────

    [Fact]
    public void SetResult_ComplexType_RoundTrips()
    {
        var accessor = MakeAccessor(out _);
        accessor.SetCurrentJob(Guid.NewGuid(), "job:complex");

        var value = new List<int> { 1, 2, 3 };
        accessor.SetResult(value);

        var result = accessor.GetResult<List<int>>("job:complex");
        Assert.NotNull(result);
        Assert.Equal([1, 2, 3], result);
    }

    [Fact]
    public void SetResult_NullableValue_RoundTrips()
    {
        var accessor = MakeAccessor(out _);
        accessor.SetCurrentJob(Guid.NewGuid(), "job:nullable");

        accessor.SetResult<string?>(null);

        Assert.Null(accessor.GetResult<string>("job:nullable"));
    }
}
