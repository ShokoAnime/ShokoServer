using System.Runtime.CompilerServices;
using Shoko.Abstractions.Metadata;

namespace Shoko.Tests.Infrastructure;

/// <summary>
/// Plugin sources the tests use, registered once for the whole test run.
/// </summary>
/// <remarks>
/// The core only registers its own sources, so anything standing in for a plugin's source is
/// registered here, the way a plugin would register its own.
/// </remarks>
public static class TestSources
{
    #region Sources

    static TestSources()
    {
        Plugin = MetadataSource.Register("TestPlugin", "test-plugin");
        AniList = MetadataSource.Register("AniList", "anilist");
        LocalPlugin = MetadataSource.Register("TestLocalPlugin", "test-local-plugin", local: true);
    }

    /// <summary>
    /// Registers the sources when the test assembly loads, so a test that names one by its text
    /// alone finds it whatever ran before it.
    /// </summary>
    [ModuleInitializer]
    internal static void RegisterOnLoad()
        => RuntimeHelpers.RunClassConstructor(typeof(TestSources).TypeHandle);

    /// <summary>
    /// A generic plugin source.
    /// </summary>
    public static MetadataSource Plugin { get; }

    /// <summary>
    /// AniList, as its plugin registers it.
    /// </summary>
    public static MetadataSource AniList { get; }

    /// <summary>
    /// A plugin source registered as local, whose data the plugin makes on the server.
    /// </summary>
    public static MetadataSource LocalPlugin { get; }

    #endregion
}
