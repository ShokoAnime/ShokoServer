using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Newtonsoft.Json;
using NJsonSchema;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.Plugin;
using Shoko.Server.Actions;
using Shoko.Server.Services;
using Shoko.Server.Services.Configuration;
using Xunit;

namespace Shoko.Tests.Services;

/// <summary>
///   Coverage for the checks an invocation payload goes through: the schema
///   <see cref="ShokoJsonSchemaGenerator.GetSchemaForActionParameters"/>
///   produces, and the population that follows it.
/// </summary>
/// <remarks>
///   <c>ActionService.ValidateParameters</c> is exactly these two pieces
///   composed — the action's schema plus
///   <c>IConfigurationService.Validate(json, schema)</c> — so they are driven
///   directly here. The repository has no controller-level test harness, and
///   standing one up would exercise ASP.NET's body binding rather than any of
///   this.
/// </remarks>
[Collection(ConfigurationSchemaCollection.Name)]
public class ActionParameterValidationTests
{
    private static ConfigurationService CreateConfigurationService()
    {
        var applicationPaths = new Mock<IApplicationPaths>();
        applicationPaths.SetupGet(x => x.DataPath).Returns(Path.GetTempPath());
        applicationPaths.SetupGet(x => x.ConfigurationsPath).Returns(Path.GetTempPath());
        return new ConfigurationService(NullLoggerFactory.Instance, applicationPaths.Object, Mock.Of<IPluginManager>());
    }

    private static JsonSchema SchemaFor<TAction>() where TAction : IExecutableAction
        => ShokoJsonSchemaGeneratorGoldenTests.CreateGenerator().GetSchemaForActionParameters(typeof(TAction)).Schema;

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> Validate<TAction>(string json) where TAction : IExecutableAction
        => CreateConfigurationService().Validate(json, SchemaFor<TAction>());

    [Fact]
    public void TheSchema_ClosesTheObjectSoATypoCannotSlipThrough()
    {
        // The generator leaves a configuration's objects open, which is right
        // for a document read back from disk and wrong for an invocation
        // payload.
        var schema = SchemaFor<ParameterisedGlobalAction>();

        Assert.False(schema.AllowAdditionalProperties);
        Assert.Contains("\"additionalProperties\": false", schema.ToJson(), StringComparison.Ordinal);
    }

    [Fact]
    public void AMistypedParameterName_IsRejectedRatherThanIgnored()
    {
        // The whole reason this validation is worth having: `PopulateObject`
        // ignores a member it cannot map, so without the check the action would
        // run on its defaults and report success.
        var errors = Validate<ParameterisedGlobalAction>("""{"Quury": "hello"}""");

        // Keyed by the offending path, the same way a rejected configuration
        // body is. The bracket notation is NJsonSchema's own for an
        // additional-property error and is shared with the configuration
        // endpoints, so it is pinned here rather than reshaped.
        var (path, messages) = Assert.Single(errors);
        Assert.Equal("['Quury']", path);
        Assert.Equal(["No Additional Properties Allowed"], messages);
    }

    [Fact]
    public void AMetadataFieldInTheBody_IsRejected()
    {
        // `Name` and `Permission` are not parameters, so the schema does not
        // list them and the closed object turns them into errors.
        Assert.NotEmpty(Validate<ParameterisedGlobalAction>("""{"Name": "hijacked"}"""));
        Assert.NotEmpty(Validate<ParameterisedGlobalAction>("""{"Permission": "User"}"""));
        Assert.NotEmpty(Validate<ParameterisedGlobalAction>("""{"Query": "fine", "Category": "Import"}"""));
    }

    [Fact]
    public void APartialBody_IsAccepted()
    {
        // Nothing is required of an invocation payload: the instance is already
        // built with its own defaults, so supplying one parameter must not
        // oblige the caller to supply the rest.
        Assert.Empty(Validate<ParameterisedGlobalAction>("""{"Query": "hello"}"""));
        Assert.Empty(Validate<ParameterisedGlobalAction>("{}"));
        Assert.Empty(Validate<DownloadAllImagesAction>("""{"Force": true}"""));
        Assert.Empty(Validate<PurgeAllTmdbLinksAction>("""{"RemoveShowLinks": false}"""));
    }

    [Fact]
    public void TheSchema_RequiresNothing()
    {
        var schema = SchemaFor<DownloadAllImagesAction>();

        Assert.Empty(schema.RequiredProperties);
        Assert.All(schema.ActualProperties.Values, x => Assert.False(x.IsRequired));
    }

    [Fact]
    public void AWrongTypedValue_IsRejected()
    {
        Assert.NotEmpty(Validate<ParameterisedGlobalAction>("""{"MaxResults": "lots"}"""));
        Assert.NotEmpty(Validate<ParameterisedGlobalAction>("""{"Mode": "sideways"}"""));
    }

    [Fact]
    public void AnOutOfRangeValue_IsRejected()
    {
        // `[Range(1, 100)]` reaches the schema, so the bound is enforced on the
        // way in and not only rendered in the form.
        Assert.NotEmpty(Validate<ParameterisedGlobalAction>("""{"MaxResults": 0}"""));
        Assert.NotEmpty(Validate<ParameterisedGlobalAction>("""{"MaxResults": 1000}"""));
        Assert.Empty(Validate<ParameterisedGlobalAction>("""{"MaxResults": 50}"""));
    }

    [Fact]
    public void AFullyPopulatedBody_RoundTripsOntoTheAction()
    {
        var action = new ParameterisedGlobalAction();
        var parameters = new Dictionary<string, object?>
        {
            ["Query"] = "shoko",
            ["Mode"] = "fast",
            ["MaxResults"] = 7,
            ["Tags"] = new List<string> { "a", "b" },
            ["DryRun"] = true,
        };

        Assert.Empty(Validate<ParameterisedGlobalAction>(JsonConvert.SerializeObject(parameters)));
        ActionService.PopulateParameters(action, parameters);

        Assert.Equal("shoko", action.Query);
        Assert.Equal(TwinMode.Fast, action.Mode);
        Assert.Equal(7, action.MaxResults);
        Assert.Equal(["a", "b"], action.Tags);
        Assert.True(action.DryRun);
    }

    [Fact]
    public void PopulationCannotWriteTheMetadataSurface()
    {
        // Belt and braces: validation rejects such a body before it gets here,
        // but population uses the same contract resolver that hides the
        // metadata from the schema, so the members are not writable even if a
        // payload reaches this point unchecked.
        var action = new SettableMetadataAction();
        var parameters = new Dictionary<string, object?>
        {
            ["Name"] = "hijacked",
            ["Description"] = "hijacked",
            ["RequiresConfirmation"] = false,
            ["Force"] = true,
        };

        ActionService.PopulateParameters(action, parameters);

        Assert.Equal("Settable Metadata", action.Name);
        Assert.Equal("Not a parameter.", action.Description);
        Assert.True(action.RequiresConfirmation);
        // The one genuine parameter still lands.
        Assert.True(action.Force);
    }

    [Fact]
    public void ANestedParameterObject_IsClosedToo()
    {
        // A typo one level down is just as silent as one at the top.
        Assert.Empty(Validate<NestedParameterAction>("""{"Options": {"Force": true, "Depth": 3}}"""));
        Assert.NotEmpty(Validate<NestedParameterAction>("""{"Options": {"Frce": true}}"""));
    }

    [Fact]
    public void ADictionaryParameter_StaysOpen()
    {
        // A dictionary carries its value type in `additionalProperties`, so
        // closing it would throw the value schema away and reject every entry.
        Assert.Empty(Validate<NestedParameterAction>("""{"Weights": {"anything": 3, "at-all": 7}}"""));
        // The value type is still enforced.
        Assert.NotEmpty(Validate<NestedParameterAction>("""{"Weights": {"anything": "three"}}"""));
    }

    [Fact]
    public void AnActionWithSettableMetadata_StillDoesNotListItAsAParameter()
    {
        var schema = SchemaFor<SettableMetadataAction>();

        Assert.Equal(["Force"], schema.ActualProperties.Keys);
        Assert.NotEmpty(Validate<SettableMetadataAction>("""{"Name": "hijacked"}"""));
    }
}
