using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Newtonsoft.Json.Linq;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.UI;
using Shoko.Abstractions.UI.Attributes;
using Shoko.Abstractions.UI.Components;
using Shoko.Abstractions.UI.Elements;
using Shoko.Server.Services.Configuration;
using Xunit;

namespace Shoko.Tests.Services;

/// <summary>
///   Coverage for server-listed options: the startup check on the method an
///   <see cref="OptionsProviderAttribute"/> names, where the definition points
///   a client, and how a member path is resolved and its options listed.
/// </summary>
[Collection(ConfigurationSchemaCollection.Name)]
public class UiOptionsProviderTests
{
    #region Definition

    [Fact]
    public void EveryProvidedMemberPointsAtTheRoute()
    {
        var wrapped = ShokoJsonSchemaGeneratorGoldenTests.CreateGenerator().GetSchemaForType(typeof(OptionsConfiguration));
        var definition = new UiDefinitionBuilder(NullLogger<UiDefinitionBuilder>.Instance).Build(Guid.Empty, "Options", null, wrapped, "route");
        var root = Assert.IsType<UiSectionContainerElement>(definition.Root);
        var row = Assert.IsType<UiSectionContainerElement>(Assert.IsType<UiListElement>(root.Items["Rows"]).Item);

        Assert.All(
            new[] { root.Items["Port"], root.Items["Mode"], root.Items["Tags"], row.Items["Name"] },
            element => Assert.Equal("route", element.OptionsRoute)
        );
        Assert.Null(root.Items["Plain"].OptionsRoute);
        Assert.Null(row.Items["Prefix"].OptionsRoute);
    }

    [Theory]
    [InlineData(typeof(MissingMethodConfiguration), "is not a public method of")]
    [InlineData(typeof(OverloadedMethodConfiguration), "more than one overload")]
    [InlineData(typeof(WrongElementConfiguration), "rather than a collection of Int32")]
    [InlineData(typeof(ScalarReturnConfiguration), "rather than a collection of Int32")]
    [InlineData(typeof(DictionaryMemberConfiguration), "is a dictionary")]
    public void AProviderThatDoesNotFitFailsGeneration(Type type, string fault)
    {
        var exception = Assert.Throws<NotSupportedException>(() => ShokoJsonSchemaGeneratorGoldenTests.CreateGenerator().GetSchemaForType(type));

        Assert.Contains($"{type.Name}.Value", exception.Message, StringComparison.Ordinal);
        Assert.Contains(fault, exception.Message, StringComparison.Ordinal);
    }

    #endregion

    #region Lookup

    [Fact]
    public async Task TheProviderSeesTheEditedValues()
    {
        var options = await ListAsync(new OptionsConfiguration { Port = 8 }, "Port");

        Assert.Equal([8, 9], options.Select(x => x.Value!.Value<int>()));
    }

    [Fact]
    public async Task ATaskIsAwaitedForANullableMember()
    {
        var options = await ListAsync(new OptionsConfiguration(), "Mode");

        Assert.Equal([1, 2], options.Select(x => x.Value!.Value<int>()));
    }

    [Fact]
    public async Task ALabelledOptionKeepsItsLabel()
    {
        var options = await ListAsync(new OptionsConfiguration(), "Tags");

        Assert.Equal([("a", "Alpha"), ("b", null)], options.Select(x => (x.Value!.Value<string>(), x.Label)));
    }

    [Fact]
    public async Task APathIntoAListEntryListsForThatEntry()
    {
        var configuration = new OptionsConfiguration { Rows = [new() { Prefix = "x" }, new() { Prefix = "y" }] };

        var options = await ListAsync(configuration, "Rows[1].Name");

        Assert.Equal(["y1"], options.Select(x => x.Value!.Value<string>()));
    }

    [Theory]
    [InlineData("Plain")]
    [InlineData("Missing")]
    [InlineData("Rows[3].Name")]
    [InlineData("Rows[0]")]
    public void APathNotEndingInAProvidedMemberIsRejected(string path)
        => Assert.ThrowsAny<ArgumentException>(() => UiOptionsProvider.Resolve(new OptionsConfiguration { Rows = [new()] }, path, isNewtonsoftJson: true));

    private static async Task<IReadOnlyList<UiOption>> ListAsync(OptionsConfiguration configuration, string path)
    {
        var (owner, method) = UiOptionsProvider.Resolve(configuration, path, isNewtonsoftJson: true);
        return await UiOptionsProvider.InvokeAsync(
            method,
            Mock.Of<IPluginManager>(),
            owner,
            [configuration],
            value => value is null ? null : JToken.FromObject(value)
        );
    }

    #endregion

    #region Types

    /// <summary>Options listed in every shape a provider may return them.</summary>
    public class OptionsConfiguration
    {
        /// <summary>A scalar, listed from its own value.</summary>
        [OptionsProvider(nameof(ListPorts))]
        public int Port { get; set; }

        /// <summary>A nullable scalar, listed through a task.</summary>
        [OptionsProvider(nameof(ListModesAsync))]
        public int? Mode { get; set; }

        /// <summary>A list, listed with labels.</summary>
        [OptionsProvider(nameof(ListTags))]
        public List<string> Tags { get; set; } = [];

        /// <summary>A member without options.</summary>
        public string Plain { get; set; } = string.Empty;

        /// <summary>Entries with options of their own.</summary>
        public List<OptionsRow> Rows { get; set; } = [];

        /// <summary>Lists ports around the edited one.</summary>
        public int[] ListPorts()
            => [Port, Port + 1];

        /// <summary>Lists modes.</summary>
        public Task<IReadOnlyList<int>> ListModesAsync()
            => Task.FromResult<IReadOnlyList<int>>([1, 2]);

        /// <summary>Lists tags.</summary>
        public static SelectOption<string>[] ListTags()
            => [new("a", "Alpha"), new("b")];
    }

    /// <summary>A list entry listing options from its own values.</summary>
    public class OptionsRow
    {
        /// <summary>A name, listed from the prefix.</summary>
        [OptionsProvider(nameof(ListNames))]
        public string Name { get; set; } = string.Empty;

        /// <summary>The prefix.</summary>
        public string Prefix { get; set; } = string.Empty;

        /// <summary>Lists names.</summary>
        public IEnumerable<string> ListNames()
            => [Prefix + "1"];
    }

    /// <summary>Names a method that is not there.</summary>
    public class MissingMethodConfiguration
    {
        /// <summary>The member.</summary>
        [OptionsProvider("Nope")]
        public int Value { get; set; }
    }

    /// <summary>Names an overloaded method.</summary>
    public class OverloadedMethodConfiguration
    {
        /// <summary>The member.</summary>
        [OptionsProvider(nameof(List))]
        public int Value { get; set; }

        /// <summary>One overload.</summary>
        public int[] List()
            => [];

        /// <summary>Another overload.</summary>
        public int[] List(int count)
            => new int[count];
    }

    /// <summary>Lists values of another type.</summary>
    public class WrongElementConfiguration
    {
        /// <summary>The member.</summary>
        [OptionsProvider(nameof(List))]
        public int Value { get; set; }

        /// <summary>Lists longs.</summary>
        public long[] List()
            => [];
    }

    /// <summary>Returns one value rather than a collection.</summary>
    public class ScalarReturnConfiguration
    {
        /// <summary>The member.</summary>
        [OptionsProvider(nameof(List))]
        public int Value { get; set; }

        /// <summary>Returns one value.</summary>
        public int List()
            => 0;
    }

    /// <summary>Asks for options on a dictionary.</summary>
    public class DictionaryMemberConfiguration
    {
        /// <summary>The member.</summary>
        [OptionsProvider(nameof(List))]
        public Dictionary<string, int> Value { get; set; } = [];

        /// <summary>Lists values.</summary>
        public int[] List()
            => [];
    }

    #endregion
}
