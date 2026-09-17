using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using Shoko.Abstractions.UI;
using Shoko.Abstractions.UI.Attributes;
using Shoko.Abstractions.UI.Elements;
using Shoko.Abstractions.UI.Enums;
using Shoko.Server.Services.Configuration;
using Xunit;

namespace Shoko.Tests.Services;

/// <summary>
///   Coverage for the operators a condition compares with, and for the checks
///   that stop one being authored that could never hold.
/// </summary>
[Collection(ConfigurationSchemaCollection.Name)]
public class UiConditionTests
{
    private static UiSectionContainerElement RootOf(Type type)
    {
        var wrapped = ShokoJsonSchemaGeneratorGoldenTests.CreateGenerator().GetSchemaForType(type);
        var definition = new UiDefinitionBuilder(NullLogger<UiDefinitionBuilder>.Instance).Build(Guid.Empty, "Conditions", null, wrapped);
        return Assert.IsType<UiSectionContainerElement>(definition.Root);
    }

    [Fact]
    public void Operators_ReachTheElementTheyWereAuthoredOn()
    {
        var root = RootOf(typeof(OperatorConfiguration));

        // The empty test is the one an equality check cannot express: a cleared
        // text field holds "", not null.
        var encoder = root.Items["Encoder"];
        Assert.Equal(UiConditionOperator.IsEmpty, encoder.Visibility.Disable?.Operator);
        Assert.Equal("FfmpegPath", encoder.Visibility.Disable?.Path);
        Assert.Null(encoder.Visibility.Disable?.Value);
        Assert.Null(encoder.Visibility.Disable?.Values);

        // A set, which used to need one property per value.
        var device = root.Items["RenderDevice"];
        Assert.Equal(UiConditionOperator.In, device.Visibility.Toggle?.Operator);
        // Rendered by the configuration's own serializer, the same as a single
        // value would be.
        Assert.Equal(["Vaapi", "Qsv"], device.Visibility.Toggle?.Values?.Select(x => x!.ToString()));
        Assert.Equal(DisplayVisibility.Visible, device.Visibility.Toggle?.Visibility);

        // A dotted path, resolved through the nested class.
        var localPath = root.Items["LocalPath"];
        Assert.Equal("Nested.Mode", localPath.Visibility.Disable?.Path);
        Assert.Equal(UiConditionOperator.NotEquals, localPath.Visibility.Disable?.Operator);

        // An action's conditions go through the same reader.
        var action = root.Actions["ProbeAction"];
        Assert.Equal(UiConditionOperator.IsNotEmpty, action.Toggle?.Operator);
        Assert.Equal(UiConditionOperator.GreaterThan, action.Disable?.Operator);
        Assert.Equal("4", action.Disable?.Value?.ToString());
    }

    [Theory]
    [InlineData(typeof(EmptyOperatorWithValue), "takes no value")]
    [InlineData(typeof(SetOperatorWithoutValues), "needs one or more values")]
    [InlineData(typeof(SingleOperatorWithSet), "takes a value rather than a set")]
    [InlineData(typeof(NumericOperatorOnText), "compares numbers")]
    [InlineData(typeof(ContainsOnNumber), "needs a string or a collection")]
    [InlineData(typeof(PathThatDoesNotResolve), "does not have")]
    [InlineData(typeof(PathThroughACollection), "which is a collection")]
    public void AConditionThatCouldNeverHold_FailsRatherThanRendering(Type type, string because)
    {
        // Silently emitting one leaves the element in whichever state the author
        // did not intend, with nothing saying why.
        var exception = Assert.ThrowsAny<Exception>(() => ShokoJsonSchemaGeneratorGoldenTests.CreateGenerator().GetSchemaForType(type));

        Assert.Contains(because, Flatten(exception), StringComparison.Ordinal);
    }

    private static string Flatten(Exception exception)
        => exception.InnerException is { } inner ? $"{exception.Message} {Flatten(inner)}" : exception.Message;

    /// <summary>One of every operator, authored the way a plugin would.</summary>
    public class OperatorConfiguration
    {
        /// <summary>Where ffmpeg lives.</summary>
        public string FfmpegPath { get; set; } = string.Empty;

        /// <summary>How it is accelerated.</summary>
        public HardwareAcceleration Acceleration { get; set; }

        /// <summary>How many at once.</summary>
        public int Threads { get; set; }

        /// <summary>The nested shape a dotted path descends into.</summary>
        public NestedConfiguration Nested { get; set; } = new();

        /// <summary>Unusable until there is a path to probe.</summary>
        [Visibility(DisableWhenMemberIsSet = nameof(FfmpegPath), DisableOperator = UiConditionOperator.IsEmpty)]
        public string Encoder { get; set; } = string.Empty;

        /// <summary>Only meaningful for two of the acceleration modes.</summary>
        [Visibility(
            DisplayVisibility.Hidden,
            ToggleWhenMemberIsSet = nameof(Acceleration),
            ToggleOperator = UiConditionOperator.In,
            ToggleWhenSetToAny = new object?[] { HardwareAcceleration.Vaapi, HardwareAcceleration.Qsv },
            ToggleVisibilityTo = DisplayVisibility.Visible
        )]
        public string RenderDevice { get; set; } = string.Empty;

        /// <summary>Pointless while the nested mode is remote.</summary>
        [Visibility(
            DisableWhenMemberIsSet = $"{nameof(Nested)}.{nameof(NestedConfiguration.Mode)}",
            DisableOperator = UiConditionOperator.NotEquals,
            DisableWhenSetTo = NestedMode.Local
        )]
        public string LocalPath { get; set; } = string.Empty;

        /// <summary>Probes the encoders the path offers.</summary>
        [CustomAction(
            ToggleWhenMemberIsSet = nameof(FfmpegPath),
            ToggleOperator = UiConditionOperator.IsNotEmpty,
            DisableWhenMemberIsSet = nameof(Threads),
            DisableOperator = UiConditionOperator.GreaterThan,
            DisableWhenSetTo = 4
        )]
        public void ProbeAction() { }
    }

    /// <summary>The shape a dotted path descends into.</summary>
    public class NestedConfiguration
    {
        /// <summary>Where the work happens.</summary>
        public NestedMode Mode { get; set; }
    }

    /// <summary>Where the work happens.</summary>
    public enum NestedMode
    {
        /// <summary>Here.</summary>
        Local = 0,

        /// <summary>Somewhere else.</summary>
        Remote = 1,
    }

    /// <summary>How transcoding is accelerated.</summary>
    public enum HardwareAcceleration
    {
        /// <summary>Not at all.</summary>
        None = 0,

        /// <summary>Video Acceleration API.</summary>
        Vaapi = 1,

        /// <summary>Quick Sync.</summary>
        Qsv = 2,
    }

    /// <summary>An emptiness test handed a value to compare.</summary>
    public class EmptyOperatorWithValue
    {
        /// <summary>The member compared against.</summary>
        public string Path { get; set; } = string.Empty;

        /// <summary>The member guarded.</summary>
        [Visibility(DisableWhenMemberIsSet = nameof(Path), DisableOperator = UiConditionOperator.IsEmpty, DisableWhenSetTo = "")]
        public string Guarded { get; set; } = string.Empty;
    }

    /// <summary>A set test with no set.</summary>
    public class SetOperatorWithoutValues
    {
        /// <summary>The member compared against.</summary>
        public NestedMode Mode { get; set; }

        /// <summary>The member guarded.</summary>
        [Visibility(DisableWhenMemberIsSet = nameof(Mode), DisableOperator = UiConditionOperator.In)]
        public string Guarded { get; set; } = string.Empty;
    }

    /// <summary>A single-value test handed a set.</summary>
    public class SingleOperatorWithSet
    {
        /// <summary>The member compared against.</summary>
        public NestedMode Mode { get; set; }

        /// <summary>The member guarded.</summary>
        [Visibility(DisableWhenMemberIsSet = nameof(Mode), DisableWhenSetToAny = new object?[] { NestedMode.Local, NestedMode.Remote })]
        public string Guarded { get; set; } = string.Empty;
    }

    /// <summary>A numeric comparison against text.</summary>
    public class NumericOperatorOnText
    {
        /// <summary>The member compared against.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>The member guarded.</summary>
        [Visibility(DisableWhenMemberIsSet = nameof(Name), DisableOperator = UiConditionOperator.GreaterThan, DisableWhenSetTo = 2)]
        public string Guarded { get; set; } = string.Empty;
    }

    /// <summary>A containment test against a number.</summary>
    public class ContainsOnNumber
    {
        /// <summary>The member compared against.</summary>
        public int Count { get; set; }

        /// <summary>The member guarded.</summary>
        [Visibility(DisableWhenMemberIsSet = nameof(Count), DisableOperator = UiConditionOperator.Contains, DisableWhenSetTo = 2)]
        public string Guarded { get; set; } = string.Empty;
    }

    /// <summary>A path naming a member that does not exist.</summary>
    public class PathThatDoesNotResolve
    {
        /// <summary>The member guarded.</summary>
        [Visibility(DisableWhenMemberIsSet = "Nonexistent", DisableWhenSetTo = true)]
        public string Guarded { get; set; } = string.Empty;
    }

    /// <summary>A path descending through a list.</summary>
    public class PathThroughACollection
    {
        /// <summary>The collection in the way.</summary>
        public List<NestedConfiguration> Items { get; set; } = [];

        /// <summary>The member guarded.</summary>
        [Visibility(DisableWhenMemberIsSet = "Items.Mode", DisableWhenSetTo = NestedMode.Local)]
        public string Guarded { get; set; } = string.Empty;
    }
}
