using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Namotion.Reflection;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Config.Attributes;
using Shoko.Abstractions.Config.Enums;
using Shoko.Abstractions.Config.Exceptions;
using Shoko.Abstractions.Plugin;
using Shoko.Server.Services.Configuration;
using Xunit;

namespace Shoko.Tests.Services;

/// <summary>
///   Coverage for running every handler the edited path passes through, rather
///   than only the one nearest the edit.
/// </summary>
[Collection(ConfigurationSchemaCollection.Name)]
public class ReactiveChainTests
{
    private static (ConfigurationService Service, ConfigurationInfo Info) CreateFor(Type type)
    {
        var applicationPaths = new Mock<IApplicationPaths>();
        applicationPaths.SetupGet(x => x.DataPath).Returns(Path.GetTempPath());
        applicationPaths.SetupGet(x => x.ConfigurationsPath).Returns(Path.GetTempPath());
        var service = new ConfigurationService(NullLoggerFactory.Instance, applicationPaths.Object, Mock.Of<IPluginManager>());
        var info = new ConfigurationInfo(service)
        {
            ID = Guid.NewGuid(),
            Path = null,
            Name = "Fixture",
            Description = string.Empty,
            HasCustomActions = false,
            HasCustomNewFactory = false,
            HasCustomValidation = false,
            HasCustomSave = false,
            HasCustomLoad = false,
            HasLiveEdit = true,
            Type = type,
            ContextualType = type.ToContextualType(),
            Schema = service.GenerateSchema(type),
            PluginInfo = null!,
        };
        return (service, info);
    }

    [Fact]
    public void EveryHandlerOnThePathRuns_InnermostFirst()
    {
        var (service, info) = CreateFor(typeof(ChainRoot));
        var configuration = new ChainRoot();
        ChainRoot.Trail.Clear();

        service.PerformReactiveAction(info, configuration, "Branch.Name", ConfigurationActionType.LiveEdit, ReactiveEventType.Edited);

        Assert.Equal(["branch", "root"], ChainRoot.Trail.Take(2));
    }

    [Fact]
    public void AHandlerSeesWhatTheOneBeforeItReturned()
    {
        var (service, info) = CreateFor(typeof(ChainRoot));
        var configuration = new ChainRoot();
        ChainRoot.Trail.Clear();

        service.PerformReactiveAction(info, configuration, "Branch.Name", ConfigurationActionType.LiveEdit, ReactiveEventType.Edited);

        // The root handler takes the previous result as a parameter, and records
        // whether the branch's errors were in it.
        Assert.Contains("root-saw-branch", ChainRoot.Trail);
    }

    [Fact]
    public void WhatEveryHandlerSaid_SurvivesAHandlerReturningItsOwnResult()
    {
        var (service, info) = CreateFor(typeof(ChainRoot));
        var configuration = new ChainRoot();
        ChainRoot.Trail.Clear();

        var result = service.PerformReactiveAction(info, configuration, "Branch.Name", ConfigurationActionType.LiveEdit, ReactiveEventType.Edited);

        // The root returns a fresh result rather than the one it was handed.
        Assert.NotNull(result.ValidationErrors);
        Assert.Contains("branch", result.ValidationErrors!.Keys);
        Assert.Contains("root", result.ValidationErrors!.Keys);
        // Both handlers edited the same instance, so the document holds both.
        Assert.Equal("branch-was-here", configuration.Branch.Name);
        Assert.Equal("root-was-here", configuration.Name);
    }

    [Fact]
    public void AHandlerWatchingOtherMembers_IsLeftOut()
    {
        var (service, info) = CreateFor(typeof(WatchfulChainRoot));
        var configuration = new WatchfulChainRoot();
        WatchfulChainRoot.Trail.Clear();

        service.PerformReactiveAction(info, configuration, "Branch.Name", ConfigurationActionType.LiveEdit, ReactiveEventType.Edited);

        // The root watches `Name`, so an edit under `Branch` is not its business.
        Assert.Equal(["branch"], WatchfulChainRoot.Trail);
    }

    [Fact]
    public void AHandlerWatchingTheEditedBranch_IsKept()
    {
        var (service, info) = CreateFor(typeof(WatchfulChainRoot));
        var configuration = new WatchfulChainRoot();
        WatchfulChainRoot.Trail.Clear();

        service.PerformReactiveAction(info, configuration, "Name", ConfigurationActionType.LiveEdit, ReactiveEventType.Edited);

        Assert.Equal(["root"], WatchfulChainRoot.Trail);
    }

    [Fact]
    public void APathThatDoesNotResolve_IsNotAnErrorForALiveEdit()
    {
        var (service, info) = CreateFor(typeof(ChainRoot));
        var configuration = new ChainRoot();
        ChainRoot.Trail.Clear();

        // A row being composed client-side is not in the document that was
        // posted, so the path it names does not resolve yet.
        var result = service.PerformReactiveAction(info, configuration, "Rows.[3].Name", ConfigurationActionType.LiveEdit, ReactiveEventType.NewValue);

        Assert.Empty(ChainRoot.Trail);
        Assert.Null(result.ValidationErrors);
        Assert.Empty(result.Messages);
    }

    [Fact]
    public void APathThatDoesNotResolve_StillFailsAnInvokedAction()
    {
        var (service, info) = CreateFor(typeof(ChainRoot));

        // Nobody pressed a button on a row that is not there.
        Assert.Throws<InvalidConfigurationActionException>(
            () => service.PerformCustomAction(info, new ChainRoot(), "Rows.[3]", "DoTheThing"));
    }

    /// <summary>A shape with a handler at each level.</summary>
    public class ChainRoot : IConfiguration
    {
        /// <summary>What ran, in order.</summary>
        public static readonly List<string> Trail = [];

        /// <summary>A member.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>The branch below.</summary>
        public ChainBranch Branch { get; set; } = new();

        /// <summary>A list the path can point past the end of.</summary>
        public List<ChainBranch> Rows { get; set; } = [];

        /// <summary>Runs after the branch's own handler.</summary>
        [ConfigurationAction(ConfigurationActionType.LiveEdit)]
        public ConfigurationActionResult OnEdit(ConfigurationActionContext<ChainRoot> context, ConfigurationActionResult previous)
        {
            Trail.Add("root");
            if (previous.ValidationErrors?.ContainsKey("branch") ?? false)
                Trail.Add("root-saw-branch");
            Name = "root-was-here";
            return new()
            {
                Configuration = context.Configuration,
                ValidationErrors = new Dictionary<string, IReadOnlyList<string>> { ["root"] = ["from the root"] },
            };
        }
    }

    /// <summary>The branch, which handles its own edits.</summary>
    public class ChainBranch
    {
        /// <summary>A member.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Runs first, being nearest the edit.</summary>
        [ConfigurationAction(ConfigurationActionType.LiveEdit)]
        public ConfigurationActionResult OnEdit()
        {
            ChainRoot.Trail.Add("branch");
            Name = "branch-was-here";
            return new()
            {
                ValidationErrors = new Dictionary<string, IReadOnlyList<string>> { ["branch"] = ["from the branch"] },
            };
        }
    }

    /// <summary>A shape whose root handler watches one member.</summary>
    public class WatchfulChainRoot : IConfiguration
    {
        /// <summary>What ran, in order.</summary>
        public static readonly List<string> Trail = [];

        /// <summary>The member the root watches.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>The branch below, which the root does not watch.</summary>
        public WatchfulChainBranch Branch { get; set; } = new();

        /// <summary>Runs only for an edit to what it named.</summary>
        [ConfigurationAction(ConfigurationActionType.LiveEdit, ReactiveMembers = [nameof(Name)])]
        public ConfigurationActionResult OnEdit()
        {
            Trail.Add("root");
            return new();
        }
    }

    /// <summary>The branch of the watchful shape.</summary>
    public class WatchfulChainBranch
    {
        /// <summary>A member.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Runs for any edit below it.</summary>
        [ConfigurationAction(ConfigurationActionType.LiveEdit)]
        public ConfigurationActionResult OnEdit()
        {
            WatchfulChainRoot.Trail.Add("branch");
            return new();
        }
    }
}
