using System;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Namotion.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Server.Services.Configuration;
using Shoko.Server.Settings;
using Xunit;

namespace Shoko.Tests.Services.Configuration;

/// <summary>
/// Checks that schema generators working at once do not corrupt the XML docs
/// Namotion.Reflection caches for the whole process.
/// </summary>
public class ConcurrentSchemaGenerationTests
{
    [Fact]
    public async Task GeneratorsWorkingAtOnceAllFinish()
    {
        // Namotion only rewrites the cached docs on their first read, so start from an empty cache.
        var clearCache = typeof(XmlDocsExtensions).GetMethod("ClearCache", BindingFlags.Static | BindingFlags.NonPublic)!;
        clearCache.Invoke(null, null);
        var generators = Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
            new ShokoJsonSchemaGenerator(new JsonSerializerSettings { Converters = [new StringEnumConverter()] }, new JsonSerializerOptions())
                .GetSchemaForType(typeof(ServerSettings))
        )));

        var finished = await Task.WhenAny(generators, Task.Delay(TimeSpan.FromSeconds(60), TestContext.Current.CancellationToken));
        Assert.Same(generators, finished);
    }
}
