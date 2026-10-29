using System;
using System.Collections.Concurrent;
using System.Linq;
using Newtonsoft.Json.Serialization;
using NLog;
using Shoko.Server.Utilities;

namespace Shoko.Server.Databases.NHibernate;

public class SimpleNameSerializationBinder : DefaultSerializationBinder
{
    private readonly Logger _logger = LogManager.GetCurrentClassLogger();
    private readonly Type? _baseType;

    // The scannable types never change once the server is up, so each name is looked up once.
    private readonly ConcurrentDictionary<string, Type?> _typesByName = new();

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
        => _typesByName.GetOrAdd(typeName.Split('.')[^1], FindType)!;

    private Type? FindType(string name)
    {
        var types = ReflectionUtils.ScannableTypes()
            .Where(a => a.Name.Equals(name) && (_baseType == null || _baseType.IsAssignableFrom(a))).ToArray();
        if (types.Length > 1) _logger.Warn($"SimpleNameSerializationBinder found multiple types that match {name}");
        return types.FirstOrDefault();
    }
}
