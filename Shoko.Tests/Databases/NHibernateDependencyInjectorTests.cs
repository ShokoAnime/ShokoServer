using System.Collections.Concurrent;
using System.Reflection;
using Shoko.Server.Databases.NHibernate;
using Xunit;

namespace Shoko.Tests.Databases;

/// <summary>
/// Guards against reverting <see cref="NHibernateDependencyInjector"/>'s static type-info caches
/// back to plain <see cref="System.Collections.Generic.Dictionary{TKey,TValue}"/>. <c>Instantiate</c>
/// runs concurrently across NHibernate sessions on different worker threads; a plain Dictionary
/// corrupts under concurrent first-time writes for the same not-yet-cached key, throwing
/// "Operations that change non-concurrent collections must have exclusive access" and leaving the
/// dictionary permanently broken (every subsequent lookup throws) for the rest of the process.
/// </summary>
public class NHibernateDependencyInjectorTests
{
    private static readonly BindingFlags StaticNonPublic = BindingFlags.NonPublic | BindingFlags.Static;

    [Theory]
    [InlineData("s_typeHasValidConstructors", typeof(ConcurrentDictionary<string, bool>))]
    [InlineData("s_allTypes", typeof(ConcurrentDictionary<string, System.Type>))]
    public void TypeInfoCache_IsConcurrentDictionary(string fieldName, System.Type expectedType)
    {
        var field = typeof(NHibernateDependencyInjector).GetField(fieldName, StaticNonPublic)!;
        Assert.Equal(expectedType, field.FieldType);
    }
}
