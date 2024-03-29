using System;
using Quartz;
using Shoko.Server.Scheduling.GenericJobBuilder.Utils;

namespace Shoko.Server.Scheduling.GenericJobBuilder;

public static class JobDataMapBuilder
{
    public static JobDataMap FromType<T>(Action<T> ctor) where T : class, IJob
    {
        var constructor = TypeConstructorCache.Get(typeof(T));
        if (constructor == null) throw new Exception($"Type \"{typeof(T)}\" does not have a valid constructor");
        var original = constructor.Invoke(null) as T;
        var temp = constructor.Invoke(null) as T;
        ctor.Invoke(temp);

        var map = new JobDataMap();
        var properties = TypePropertyCache.Get(typeof(T));

        foreach (var property in properties)
        {
            var originalValue = property.GetValue(original);
            var newValue = property.GetValue(temp);
            if (!Equals(newValue, originalValue)) map.Put(property.Name, newValue!);
        }

        return map;
    }
}
