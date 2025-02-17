using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Shoko.Server.Data.TypeConverters;

public class StringListToString() : ValueConverter<List<string>, string>(l => string.Join("|||", l), s => s.Split("|||", StringSplitOptions.TrimEntries).ToList());

public class StringListComparer()
    : ValueComparer<List<string>>((l1, l2) => l1.SequenceEqual(l2),
        CreateDefaultHashCodeExpression(false), CreateDefaultSnapshotExpression(false));
