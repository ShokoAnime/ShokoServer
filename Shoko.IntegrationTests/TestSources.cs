using Shoko.Abstractions.Metadata;

namespace Shoko.IntegrationTests;

/// <summary>
/// Plugin sources the integration tests use, registered the way a plugin
/// registers the sources it owns.
/// </summary>
/// <remarks>
/// Registration closes once the server has set up its plugins, so
/// <see cref="DatabaseMigrationFixture"/> touches this class before it starts
/// the server. A source a test needs is added here, never registered from the
/// test itself.
/// </remarks>
public static class TestSources
{
    #region Sources

    static TestSources()
    {
        Plugin = MetadataSource.Register("TestPlugin", "test-plugin", description: "A plugin source for the integration tests.");
        Image = MetadataSource.Register("TestImagePlugin", "test-image-plugin");
        Template = MetadataSource.Register("TestTemplatePlugin", "test-template-plugin");
        TextManagement = MetadataSource.Register("TestTextManagementPlugin", "test-text-management-plugin");
        LocalImage = MetadataSource.Register("TestLocalImagePlugin", "test-local-image-plugin", local: true);
    }

    /// <summary>
    /// A generic plugin source.
    /// </summary>
    public static MetadataSource Plugin { get; }

    /// <summary>
    /// The source the image round trips store images under.
    /// </summary>
    public static MetadataSource Image { get; }

    /// <summary>
    /// The source the image template tests register template URLs for.
    /// </summary>
    public static MetadataSource Template { get; }

    /// <summary>
    /// The source the text management routes store their entries under.
    /// </summary>
    public static MetadataSource TextManagement { get; }

    /// <summary>
    /// A local source, whose images a plugin uploads rather than fetches.
    /// </summary>
    public static MetadataSource LocalImage { get; }

    /// <summary>
    /// Runs the registrations above, if they haven't run yet.
    /// </summary>
    public static void EnsureRegistered() { }

    #endregion
}
