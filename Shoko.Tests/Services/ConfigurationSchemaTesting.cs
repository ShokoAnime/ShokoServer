using Shoko.Server.Services.Configuration;
using Xunit;

namespace Shoko.Tests.Services;

/// <summary>
///   Builds the schema generator the tests share.
/// </summary>
internal static class TestSchemaGenerator
{
    /// <summary>
    ///   Creates a generator set up with the server's own serializers.
    /// </summary>
    /// <returns>The generator.</returns>
    public static ShokoJsonSchemaGenerator Create()
        => new(ShokoJsonSerializers.CreateNewtonsoftSettings(), ShokoJsonSerializers.CreateSystemTextJsonOptions());
}

/// <summary>
///   Groups every test that drives <see cref="ShokoJsonSchemaGenerator"/> into
///   one xUnit collection.
/// </summary>
/// <remarks>
///   Generating two schemas at once races inside NJsonSchema's and
///   Namotion.Reflection's shared XML-documentation caches and intermittently
///   drops descriptions. The server generates schemas serially under a lock, so
///   this only ever bites the test host.
/// </remarks>
[CollectionDefinition(Name, DisableParallelization = true)]
public class ConfigurationSchemaCollection
{
    /// <summary>The collection's name.</summary>
    public const string Name = "ConfigurationSchema";
}
