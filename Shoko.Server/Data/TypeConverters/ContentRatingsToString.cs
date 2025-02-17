using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Shoko.Server.Models.TMDB;

namespace Shoko.Server.Data.TypeConverters;

public class ContentRatingsToString() : ValueConverter<List<TMDB_ContentRating>, string>(l => string.Join("|", l.Select(r => r.ToString())),
    i => i.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(TMDB_ContentRating.FromString).ToList());

public class ContentRatingsComparer : ValueComparer<List<TMDB_ContentRating>>
{

    public ContentRatingsComparer() : base((l1, l2) => l1.SequenceEqual(l2), o => GetHash(o))
    {
    }
    
    private static int GetHash(List<TMDB_ContentRating> source)
    {
        var hash = new HashCode();

        foreach (var el in source)
        {
            hash.Add(el == null ? 0 : el.GetHashCode());
        }

        return hash.ToHashCode();
    }
}
