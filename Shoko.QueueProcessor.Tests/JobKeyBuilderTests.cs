using System;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Builder;
using Xunit;

namespace Shoko.QueueProcessor.Tests;

/// <summary>Tests for <see cref="JobKeyBuilder{T}"/> key generation.</summary>
public class JobKeyBuilderTests
{
    // ── Helper job types ──────────────────────────────────────────────────────

    private class SimpleJob : IQueueJob
    {
        public string TypeName => "SimpleJob";
        public string Title => "Simple";
        public System.Collections.Generic.Dictionary<string, object> Details => [];
        public void PostInit() { }
        public System.Threading.Tasks.Task Process() => System.Threading.Tasks.Task.CompletedTask;

        public string FilePath { get; set; } = string.Empty;
        public bool ForceHash { get; set; }
        public int AnimeId { get; set; }
    }

    [JobKeyGroup("Import")]
    private class GroupedJob : IQueueJob
    {
        public string TypeName => "GroupedJob";
        public string Title => "Grouped";
        public System.Collections.Generic.Dictionary<string, object> Details => [];
        public void PostInit() { }
        public System.Threading.Tasks.Task Process() => System.Threading.Tasks.Task.CompletedTask;

        public int SeriesId { get; set; }
    }

    private class AnnotatedJob : IQueueJob
    {
        public string TypeName => "AnnotatedJob";
        public string Title => "Annotated";
        public System.Collections.Generic.Dictionary<string, object> Details => [];
        public void PostInit() { }
        public System.Threading.Tasks.Task Process() => System.Threading.Tasks.Task.CompletedTask;

        // Explicit annotation with stable id and ordering
        [JobKeyMember("path", index: 0)]
        public string FilePath { get; set; } = string.Empty;

        [JobKeyMember("force", index: 1)]
        public bool ForceHash { get; set; }

        // Not a key member — should not appear in the key
        public string InternalState { get; set; } = string.Empty;
    }

    private class ClassPrefixJob : IQueueJob
    {
        public string TypeName => "ClassPrefixJob";
        public string Title => "";
        public System.Collections.Generic.Dictionary<string, object> Details => [];
        public void PostInit() { }
        public System.Threading.Tasks.Task Process() => System.Threading.Tasks.Task.CompletedTask;

        [JobKeyMember("id")]
        public int AnimeId { get; set; }
    }

    [JobKeyMember("CustomPrefix")]
    private class CustomPrefixJob : IQueueJob
    {
        public string TypeName => "CustomPrefixJob";
        public string Title => "";
        public System.Collections.Generic.Dictionary<string, object> Details => [];
        public void PostInit() { }
        public System.Threading.Tasks.Task Process() => System.Threading.Tasks.Task.CompletedTask;

        [JobKeyMember("id")]
        public int AnimeId { get; set; }
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Build_NoData_UsesTypeName()
    {
        var key = JobKeyBuilder<SimpleJob>.Create().Build();
        Assert.StartsWith("SimpleJob", key);
    }

    [Fact]
    public void Build_WithData_IncludesValues()
    {
        var key = JobKeyBuilder<SimpleJob>.Create()
            .UsingJobData(j => j.FilePath = "/anime/ep01.mkv")
            .Build();

        Assert.Contains("FilePath", key);
        Assert.Contains("ep01.mkv", key);
    }

    [Fact]
    public void Build_SameData_ProducesSameKey()
    {
        var key1 = JobKeyBuilder<SimpleJob>.Create()
            .UsingJobData(j => { j.FilePath = "/path/file.mkv"; j.ForceHash = true; })
            .Build();
        var key2 = JobKeyBuilder<SimpleJob>.Create()
            .UsingJobData(j => { j.FilePath = "/path/file.mkv"; j.ForceHash = true; })
            .Build();

        Assert.Equal(key1, key2);
    }

    [Fact]
    public void Build_DifferentData_ProducesDifferentKeys()
    {
        var key1 = JobKeyBuilder<SimpleJob>.Create()
            .UsingJobData(j => j.FilePath = "/path/file1.mkv")
            .Build();
        var key2 = JobKeyBuilder<SimpleJob>.Create()
            .UsingJobData(j => j.FilePath = "/path/file2.mkv")
            .Build();

        Assert.NotEqual(key1, key2);
    }

    [Fact]
    public void Build_DefaultValues_ExcludedFromKey()
    {
        // When a property has its default value (bool=false, int=0), it doesn't appear in key
        var keyWithDefault = JobKeyBuilder<SimpleJob>.Create()
            .UsingJobData(j => j.FilePath = "/file.mkv")
            .Build();

        var keyWithExplicitFalse = JobKeyBuilder<SimpleJob>.Create()
            .UsingJobData(j => { j.FilePath = "/file.mkv"; j.ForceHash = false; })
            .Build();

        // Both should be equal — default false == not set
        Assert.Equal(keyWithDefault, keyWithExplicitFalse);
    }

    [Fact]
    public void Build_GroupedJob_HasGroupPrefix()
    {
        var key = JobKeyBuilder<GroupedJob>.Create()
            .UsingJobData(j => j.SeriesId = 42)
            .Build();

        Assert.StartsWith("Import/", key);
    }

    [Fact]
    public void Build_AnnotatedMembers_UseAttributeIds()
    {
        var key = JobKeyBuilder<AnnotatedJob>.Create()
            .UsingJobData(j => { j.FilePath = "/test.mkv"; j.ForceHash = true; })
            .Build();

        // Attribute id "path" instead of "FilePath"
        Assert.Contains("path:", key);
        Assert.Contains("force:", key);
        // InternalState is not annotated as [JobKeyMember] and has no annotation at all on any prop,
        // but annotated job has 2 explicit [JobKeyMember]s, so only those are used
        Assert.DoesNotContain("InternalState", key);
    }

    [Fact]
    public void Build_AnnotatedOrdering_RespectIndex()
    {
        var key = JobKeyBuilder<AnnotatedJob>.Create()
            .UsingJobData(j => { j.FilePath = "/test.mkv"; j.ForceHash = true; })
            .Build();

        // path (index=0) must appear before force (index=1)
        var pathPos = key.IndexOf("path:", StringComparison.Ordinal);
        var forcePos = key.IndexOf("force:", StringComparison.Ordinal);
        Assert.True(pathPos < forcePos);
    }

    [Fact]
    public void Build_ClassLevelAttribute_OverridesTypeName()
    {
        var key = JobKeyBuilder<CustomPrefixJob>.Create()
            .UsingJobData(j => j.AnimeId = 1234)
            .Build();

        Assert.StartsWith("CustomPrefix", key);
        Assert.DoesNotContain("CustomPrefixJob", key);
    }

    [Fact]
    public void Build_UnchangedProperty_NotIncludedInKey()
    {
        // AnimeId = 0 is the default — should not be included
        var keyNoData = JobKeyBuilder<ClassPrefixJob>.Create().Build();
        var keyWithDefault = JobKeyBuilder<ClassPrefixJob>.Create()
            .UsingJobData(j => j.AnimeId = 0)
            .Build();

        Assert.Equal(keyNoData, keyWithDefault);
    }

    [Fact]
    public void Build_NonZeroInt_IncludedInKey()
    {
        var key = JobKeyBuilder<ClassPrefixJob>.Create()
            .UsingJobData(j => j.AnimeId = 9999)
            .Build();

        Assert.Contains("9999", key);
    }
}
