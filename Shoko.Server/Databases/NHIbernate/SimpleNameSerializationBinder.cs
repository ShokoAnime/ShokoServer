using System;
using System.Linq;
using Newtonsoft.Json.Serialization;
using NLog;

namespace Shoko.Server.Databases.NHibernate;

public class SimpleNameSerializationBinder : DefaultSerializationBinder
{
    private readonly Logger _logger = LogManager.GetCurrentClassLogger();
    private readonly Type? _baseType;

    public SimpleNameSerializationBinder(Type? baseType = null)
    {
        _baseType = baseType;
    }

    public override void BindToName(
        Type serializedType, out string? assemblyName, out string? typeName)
    {
        assemblyName = null;
        typeName = serializedType.Name;
    }

    public override Type BindToType(string? assemblyName, string typeName)
    {
        var name = typeName.Split('.').LastOrDefault();
        // Skipping assemblies emitted at runtime: `GetTypes()` throws `ReflectionTypeLoadException`
        // on one that is still being written to, and nothing bound here is ever emitted at runtime.
        var types = AppDomain.CurrentDomain.GetAssemblies().Where(a => !a.IsDynamic).SelectMany(a => a.GetTypes())
            .Where(a => a.Name.Equals(name) && (_baseType == null || _baseType.IsAssignableFrom(a))).ToArray();
        if (types.Length > 1) _logger.Warn($"SimpleNameSerializationBinder found multiple types that match {name}");
        return types.FirstOrDefault()!;
    }
}
