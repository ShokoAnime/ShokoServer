using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging.Abstractions;
using Shoko.Abstractions.Metadata;
using Shoko.Server.Plugin;
using Shoko.Tests.Infrastructure;
using Shoko.Tests.Plugin;
using Xunit;

namespace Shoko.Tests.Metadata;

/// <summary>
/// Covers closing registration of <see cref="MetadataSource"/> and
/// <see cref="MetadataEntityType"/> once every plugin is set up: what still
/// works, what throws, and that plugin setup closes it even when it fails.
/// </summary>
/// <remarks>
/// Closing registration is process-global, so these tests run alone and open
/// it again before they finish.
/// </remarks>
[Collection(nameof(MetadataRegistrationCollection))]
public class MetadataRegistrationFreezeTests
{
    #region Sources

    [Fact]
    public void RegisteringASourceAfterTheFreezeThrows()
    {
        var registered = MetadataSource.Register("MrfBefore", "mrf-before");
        try
        {
            MetadataSource.Freeze();

            Assert.Throws<InvalidOperationException>(() => MetadataSource.Register("MrfAfter", "mrf-after"));

            // Even a registration that would change nothing is refused.
            Assert.Throws<InvalidOperationException>(() => MetadataSource.Register("MrfBefore", "mrf-before"));
            Assert.Throws<InvalidOperationException>(() => MetadataSource.Register("AniDB", "anidb"));
        }
        finally
        {
            MetadataSource.Unfreeze();
        }

        Assert.False(MetadataSource.TryGet("mrf-after", out _));
        Assert.Same(registered, MetadataSource.Get("mrf-before"));
    }

    [Fact]
    public void LookingUpAndParsingSourcesStillWorkAfterTheFreeze()
    {
        var registered = MetadataSource.Register("MrfLookup", "mrf-lookup", ["mrf-lookup-alias"]);
        try
        {
            MetadataSource.Freeze();

            Assert.True(MetadataSource.IsFrozen);
            Assert.Same(registered, MetadataSource.Get("MRF_LOOKUP"));
            Assert.True(MetadataSource.TryGet("mrf-lookup-alias", out var found));
            Assert.Same(registered, found);
            Assert.Same(MetadataSource.AniDB, MetadataSource.Parse("AniDB"));
            Assert.Contains(registered, MetadataSource.All);

            var parsed = MetadataSource.Parse("mrf-parsed-while-frozen");
            Assert.False(parsed.IsRegistered);
        }
        finally
        {
            MetadataSource.Unfreeze();
        }

        Assert.False(MetadataSource.IsFrozen);
    }

    #endregion

    #region Entity Types

    [Fact]
    public void RegisteringAKindAfterTheFreezeThrows()
    {
        var registered = MetadataEntityType.Register("MrfKindBefore", "mrf-kind-before");
        try
        {
            MetadataEntityType.Freeze();

            Assert.Throws<InvalidOperationException>(() => MetadataEntityType.Register("MrfKindAfter", "mrf-kind-after"));
            Assert.Throws<InvalidOperationException>(() => MetadataEntityType.Register("MrfKindBefore", "mrf-kind-before"));
            Assert.Throws<InvalidOperationException>(() => MetadataEntityType.Register("Series", "series"));
        }
        finally
        {
            MetadataEntityType.Unfreeze();
        }

        Assert.False(MetadataEntityType.TryGet("mrf-kind-after", out _));
        Assert.Same(registered, MetadataEntityType.Get("mrf-kind-before"));
    }

    [Fact]
    public void LookingUpAndParsingKindsStillWorkAfterTheFreeze()
    {
        var registered = MetadataEntityType.Register("MrfKindLookup", "mrf-kind-lookup", ["mrf-kind-lookup-alias"]);
        try
        {
            MetadataEntityType.Freeze();

            Assert.True(MetadataEntityType.IsFrozen);
            Assert.Same(registered, MetadataEntityType.Get("MRF_KIND_LOOKUP"));
            Assert.True(MetadataEntityType.TryGet("mrf_kind_lookup_alias", out var found));
            Assert.Same(registered, found);
            Assert.Same(MetadataEntityType.Series, MetadataEntityType.Parse("show"));
            Assert.Contains(registered, MetadataEntityType.All);
            Assert.False(MetadataEntityType.Parse("mrf-kind-parsed-while-frozen").IsRegistered);
        }
        finally
        {
            MetadataEntityType.Unfreeze();
        }

        Assert.False(MetadataEntityType.IsFrozen);
    }

    #endregion

    #region Plugin Setup

    [Fact]
    public void SettingUpThePluginsClosesRegistration()
    {
        var manager = TestPluginManager.Create();
        try
        {
            manager.SetupPlugins();

            Assert.True(MetadataSource.IsFrozen);
            Assert.True(MetadataEntityType.IsFrozen);
        }
        finally
        {
            Unfreeze();
        }
    }

    [Fact]
    public void SettingUpThePluginsClosesRegistrationEvenWhenAPluginFails()
    {
        var manager = TestPluginManager.Create();
        // No service provider is set in the unit tests, so the plugin's setup throws on reaching for it.
        var failing = PluginTestDoubles.InstalledPluginInfo(typeof(PluginTestDoubles.TestPlugin), Guid.NewGuid(), plugin: new PluginTestDoubles.TestPlugin());
        TestPluginManager.Plugins(manager).Add(failing);
        try
        {
            Assert.Throws<AggregateException>(manager.SetupPlugins);

            Assert.True(MetadataSource.IsFrozen);
            Assert.True(MetadataEntityType.IsFrozen);
        }
        finally
        {
            Unfreeze();
        }
    }

    [Fact]
    public void ClosingRegistrationTwiceIsHarmless()
    {
        try
        {
            PluginManager.CloseMetadataRegistration(NullLogger.Instance);
            PluginManager.CloseMetadataRegistration(NullLogger.Instance);

            Assert.True(MetadataSource.IsFrozen);
            Assert.True(MetadataEntityType.IsFrozen);
        }
        finally
        {
            Unfreeze();
        }
    }

    private static void Unfreeze()
    {
        MetadataSource.Unfreeze();
        MetadataEntityType.Unfreeze();
    }

    #endregion
}

/// <summary>
/// Runs the tests that close registration of metadata sources and entity
/// types alone, since every other test may still register its own.
/// </summary>
[CollectionDefinition(nameof(MetadataRegistrationCollection), DisableParallelization = true)]
public sealed class MetadataRegistrationCollection;
