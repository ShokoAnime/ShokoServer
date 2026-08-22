using System.Collections.Generic;
using Shoko.Abstractions.Config;

namespace Shoko.Tests.Services;

/// <summary>A list of lists.</summary>
public class NestedListOfListConfiguration : INewtonsoftJsonConfiguration
{
    /// <summary>The offending property.</summary>
    public List<List<string>> Values { get; set; } = [];
}

/// <summary>A list of dictionaries.</summary>
public class NestedListOfRecordConfiguration : INewtonsoftJsonConfiguration
{
    /// <summary>The offending property.</summary>
    public List<Dictionary<string, string>> Values { get; set; } = [];
}

/// <summary>A dictionary of lists.</summary>
public class NestedRecordOfListConfiguration : INewtonsoftJsonConfiguration
{
    /// <summary>A legitimate shape: the two levels get distinct keys.</summary>
    public Dictionary<string, List<string>> Values { get; set; } = [];
}

/// <summary>A dictionary of dictionaries.</summary>
public class NestedRecordOfRecordConfiguration : INewtonsoftJsonConfiguration
{
    /// <summary>The offending property.</summary>
    public Dictionary<string, Dictionary<string, string>> Values { get; set; } = [];
}

/// <summary>An array of arrays, for the non-generic path.</summary>
public class NestedArrayOfArrayConfiguration : INewtonsoftJsonConfiguration
{
    /// <summary>The offending property.</summary>
    public string[][] Values { get; set; } = [];
}

/// <summary>The supported way to write the same thing.</summary>
public class WrappedNestedCollectionConfiguration : INewtonsoftJsonConfiguration
{
    /// <summary>The inner collection, wrapped in a class.</summary>
    public List<NestedCollectionRow> Rows { get; set; } = [];
}

/// <summary>A row holding the inner collection.</summary>
public class NestedCollectionRow
{
    /// <summary>The inner collection.</summary>
    public List<string> Values { get; set; } = [];
}
