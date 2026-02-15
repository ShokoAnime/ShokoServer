using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using NHibernate;
using NHibernate.Type;

namespace Shoko.Server.Databases.NHibernate;

public class NHibernateDependencyInjector : EmptyInterceptor
{
    private readonly IServiceProvider _provider;
    private ISession _session;
    private static readonly Dictionary<Type, Func<object, (string name, object value)[], bool>> s_postInitializationCallbacks = new();
    private static Dictionary<string, Type> s_allTypes;
    private static readonly Dictionary<string, bool> s_typeHasValidConstructors = new();

    public NHibernateDependencyInjector(IServiceProvider provider)
    {
        _provider = provider;
    }

    public override void SetSession(ISession session)
    {
        _session = session;
    }

    /// <summary>
    /// The entity passed into this has not been initialized. The tuples provided are a map of names to values for the data that will be set.
    /// </summary>
    /// <param name="action"></param>
    /// <typeparam name="T"></typeparam>
    public static void RegisterPostInitializationCallback<T>(Func<T, (string name, object value)[], bool> action) where T : class
    {
        s_postInitializationCallbacks.Add(typeof(T), (x, tuples) => action((T)x, tuples));
    }

    public override object Instantiate(string clazz, object id)
    {
        // return null -> use default NHibernate entity creation
        s_allTypes ??= AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => a.GetTypes()).DistinctBy(a => a.FullName).ToDictionary(a => a.FullName, a => a);
        if (!s_allTypes.TryGetValue(clazz, out var type)) return null;
        if (!s_typeHasValidConstructors.TryGetValue(clazz, out var hasParameters))
        {
            hasParameters = type.GetConstructors().Any(x => x.GetParameters().Any());
            s_typeHasValidConstructors.Add(clazz, hasParameters);
        }

        if (!hasParameters) return null;

        var instance = ActivatorUtilities.CreateInstance(_provider, type);
        var md = _session?.SessionFactory?.GetClassMetadata(type);
        md ??= _provider.GetRequiredService<DatabaseFactory>().SessionFactory?.GetClassMetadata(type);
        md?.SetIdentifier(instance, id);

        return instance;
    }

    public override bool OnLoad(object entity, object id, object[] state, string[] propertyNames, IType[] types)
    {
        var type = entity.GetType();
        foreach (var (key, value) in s_postInitializationCallbacks)
        {
            if (!key.IsAssignableFrom(type) && !type.IsAssignableFrom(key)) continue;
            if (value(entity, propertyNames.Zip(state).ToArray())) return true;
            return false;
        }

        return base.OnLoad(entity, id, state, propertyNames, types);
    }
}
