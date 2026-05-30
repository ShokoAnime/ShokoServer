using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Builder;
using Xunit;

namespace Shoko.QueueProcessor.Tests;

/// <summary>Tests for <see cref="JobDataSerializer"/> roundtrip serialization.</summary>
public class JobDataSerializerTests
{
    private class FullJob : IQueueJob
    {
        public string TypeName => "FullJob";
        public string Title => "Full";
        public Dictionary<string, object> Details => [];
        public void PostInit() { }
        public Task Process() => Task.CompletedTask;

        public string FilePath { get; set; } = string.Empty;
        public bool ForceHash { get; set; }
        public int AnimeId { get; set; }
        public int? NullableId { get; set; }

        [JsonIgnore]
        public string IgnoredProp { get; set; } = "should-not-serialize";
    }

    private class ReadOnlyJob : IQueueJob
    {
        public string TypeName => "ReadOnlyJob";
        public string Title => "";
        public Dictionary<string, object> Details => [];
        public void PostInit() { }
        public Task Process() => Task.CompletedTask;

        public string Name { get; set; } = string.Empty;
        // Read-only — should not be serialized or deserialized
        public string ComputedValue => Name + "_computed";
    }

    [Fact]
    public void Serialize_AllPublicSettableProps_IncludedInJson()
    {
        var job = new FullJob { FilePath = "/test.mkv", ForceHash = true, AnimeId = 42 };
        var json = JobDataSerializer.Serialize(job);

        Assert.Contains("FilePath", json);
        Assert.Contains("/test.mkv", json);
        Assert.Contains("ForceHash", json);
        Assert.Contains("AnimeId", json);
    }

    [Fact]
    public void Serialize_JsonIgnoredProp_NotInJson()
    {
        var job = new FullJob { IgnoredProp = "secret" };
        var json = JobDataSerializer.Serialize(job);

        Assert.DoesNotContain("IgnoredProp", json);
        Assert.DoesNotContain("secret", json);
    }

    [Fact]
    public void Apply_RestoresAllValues()
    {
        var original = new FullJob { FilePath = "/anime/ep.mkv", ForceHash = true, AnimeId = 123, NullableId = 456 };
        var json = JobDataSerializer.Serialize(original);

        var restored = new FullJob();
        JobDataSerializer.Apply(restored, json);

        Assert.Equal("/anime/ep.mkv", restored.FilePath);
        Assert.True(restored.ForceHash);
        Assert.Equal(123, restored.AnimeId);
        Assert.Equal(456, restored.NullableId);
    }

    [Fact]
    public void Apply_NullJson_LeavesDefaults()
    {
        var job = new FullJob { FilePath = "/original.mkv" };
        JobDataSerializer.Apply(job, null);

        Assert.Equal("/original.mkv", job.FilePath);  // unchanged
    }

    [Fact]
    public void Apply_EmptyJson_LeavesDefaults()
    {
        var job = new FullJob { FilePath = "/original.mkv" };
        JobDataSerializer.Apply(job, "");

        Assert.Equal("/original.mkv", job.FilePath);  // unchanged
    }

    [Fact]
    public void Apply_NullableProperty_RestoredCorrectly()
    {
        var original = new FullJob { NullableId = null };
        var json = JobDataSerializer.Serialize(original);

        var restored = new FullJob { NullableId = 999 };
        JobDataSerializer.Apply(restored, json);

        Assert.Null(restored.NullableId);
    }

    [Fact]
    public void Serialize_ReadOnlyProperty_NotSerialized()
    {
        var job = new ReadOnlyJob { Name = "test" };
        var json = JobDataSerializer.Serialize(job);

        Assert.DoesNotContain("ComputedValue", json);
        Assert.DoesNotContain("_computed", json);
    }

    [Fact]
    public void Apply_ReadOnlyProperty_NotApplied()
    {
        // Manually craft JSON that includes a read-only property
        var json = "{\"Name\":\"newname\",\"ComputedValue\":\"injected_computed\"}";
        var job = new ReadOnlyJob { Name = "original" };
        JobDataSerializer.Apply(job, json);

        Assert.Equal("newname", job.Name);
        // ComputedValue is derived; check it reflects Name (not the JSON value)
        Assert.Equal("newname_computed", job.ComputedValue);
    }

    [Fact]
    public void DiffFromDefault_OnlyChangedPropsReturned()
    {
        var diff = JobDataSerializer.DiffFromDefault<FullJob>(j =>
        {
            j.FilePath = "/changed.mkv";
            // ForceHash and AnimeId stay at defaults
        });

        Assert.True(diff.ContainsKey("FilePath"));
        Assert.False(diff.ContainsKey("ForceHash"));
        Assert.False(diff.ContainsKey("AnimeId"));
    }

    [Fact]
    public void DiffFromDefault_ExplicitDefaultValue_ExcludedFromDiff()
    {
        var diff = JobDataSerializer.DiffFromDefault<FullJob>(j =>
        {
            j.AnimeId = 0;  // same as default int value
        });

        Assert.False(diff.ContainsKey("AnimeId"));
    }
}
