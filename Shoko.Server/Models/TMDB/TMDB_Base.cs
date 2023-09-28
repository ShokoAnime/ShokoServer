
using System;
using System.Collections.Generic;

namespace Shoko.Server.Models.TMDB;

public abstract class TMDB_Base
{
    /// <summary>
    /// Entity Id.
    /// </summary>
    public abstract int Id { get; }

    public bool UpdateProperty<T>(T target, T value, Action<T> setter)
    {
        if (!EqualityComparer<T>.Default.Equals(target, value))
        {
            setter(value);
            return true;
        }
        return false;
    }

    public bool UpdateProperty<T1, T2>(T1 target, T2 value, Action<T1> setter, Func<T2, T1> converter)
    {
        var convertedValue = converter(value);
        if (!EqualityComparer<T1>.Default.Equals(target, convertedValue))
        {
            setter(convertedValue);
            return true;
        }
        return false;
    }
}
