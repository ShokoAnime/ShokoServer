
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace Shoko.Server.Models.TMDB;

public abstract class TMDB_Base<TId>
{
    /// <summary>
    /// Entity Id.
    /// </summary>
    [NotMapped]
    public abstract TId Id { get; }

    public bool UpdateProperty<T>(T target, T value, Action<T> setter)
    {
        if (!EqualityComparer<T>.Default.Equals(target, value))
        {
            setter(value);
            return true;
        }
        return false;
    }

    public bool UpdateProperty<T>(T target, T value, Action<T> setter, Func<T, T, bool> areEquals)
    {
        if (!areEquals(target, value))
        {
            setter(value);
            return true;
        }
        return false;
    }
}
