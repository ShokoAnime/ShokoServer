using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Shoko.QueueProcessor.Abstractions;
using Xunit;

namespace Shoko.QueueProcessor.Tests;

/// <summary>Tests for <see cref="QueueJobTypeRegistry"/> freeze-on-first-read behavior.</summary>
public class QueueJobTypeRegistryTests
{
    private class JobA : IQueueJob
    {
        public string TypeName => nameof(JobA);
        public string Title => "A";
        public Dictionary<string, object> Details => [];
        public void PostInit() { }
        public Task Process() => Task.CompletedTask;
    }

    private class JobB : IQueueJob
    {
        public string TypeName => nameof(JobB);
        public string Title => "B";
        public Dictionary<string, object> Details => [];
        public void PostInit() { }
        public Task Process() => Task.CompletedTask;
    }

    [Fact]
    public void Add_BeforeFirstRead_AppendsTypes()
    {
        var registry = new QueueJobTypeRegistry();

        registry.Add([typeof(JobA)]);
        registry.Add([typeof(JobB)]);

        Assert.Equal(new[] { typeof(JobA), typeof(JobB) }, registry.JobTypes.ToArray());
    }

    [Fact]
    public void Add_AfterFirstRead_Throws()
    {
        var registry = new QueueJobTypeRegistry();
        registry.Add([typeof(JobA)]);

        _ = registry.JobTypes; // freeze

        Assert.Throws<InvalidOperationException>(() => registry.Add([typeof(JobB)]));
    }

    [Fact]
    public void JobTypes_ReturnsSnapshot_NotMutableReference()
    {
        var registry = new QueueJobTypeRegistry();
        registry.Add([typeof(JobA)]);

        var snapshot = registry.JobTypes;
        Assert.Single(snapshot);
        Assert.IsNotType<List<Type>>(snapshot);
    }
}
