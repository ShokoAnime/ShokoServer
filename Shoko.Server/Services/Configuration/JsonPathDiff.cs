using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Shoko.Server.Services.Configuration;

/// <summary>
///   Tells which JSON paths differ between two stored documents, without ever
///   handing out a value.
/// </summary>
public static class JsonPathDiff
{
    /// <summary>
    ///   The name of the property the service adds to point at the schema,
    ///   which is not part of the configuration.
    /// </summary>
    private const string SchemaProperty = "$schema";

    /// <summary>
    ///   Gets the paths whose values differ between the two documents.
    /// </summary>
    /// <param name="previousJson">
    ///   The document stored before, or <c>null</c> when there was
    ///   none, in which case every leaf of <paramref name="currentJson"/> is
    ///   reported.
    /// </param>
    /// <param name="currentJson">The document stored now.</param>
    /// <returns>
    ///   The paths of the values changed, added or removed, in document order,
    ///   such as <c>Web.Port</c>. An object or array only one side has is
    ///   reported through its leaves; an array whose length changed is
    ///   reported as a whole.
    /// </returns>
    public static IReadOnlyList<string> GetChangedPaths(string? previousJson, string currentJson)
    {
        var paths = new List<string>();
        Compare(Parse(previousJson), Parse(currentJson), paths);
        return paths;
    }

    private static JToken? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        // Dates are compared as the text they are stored as.
        using var reader = new JsonTextReader(new System.IO.StringReader(json)) { DateParseHandling = DateParseHandling.None };
        var token = JToken.Load(reader);
        if (token is JObject obj)
            obj.Remove(SchemaProperty);
        return token;
    }

    private static void Compare(JToken? before, JToken? after, List<string> paths)
    {
        switch (before, after)
        {
            case (null, null):
                return;

            case (JObject left, JObject right):
                foreach (var name in right.Properties().Select(p => p.Name).Concat(left.Properties().Select(p => p.Name)).Distinct())
                    Compare(left.Property(name)?.Value, right.Property(name)?.Value, paths);
                return;

            case (JArray left, JArray right) when left.Count == right.Count:
                for (var i = 0; i < left.Count; i++)
                    Compare(left[i], right[i], paths);
                return;

            case (JArray left, JArray right):
                paths.Add(right.Path);
                return;

            case (null, JContainer container) when container.HasValues:
                AddLeaves(container, paths);
                return;

            case (JContainer container, null) when container.HasValues:
                AddLeaves(container, paths);
                return;

            default:
                if (before is null || after is null || !JToken.DeepEquals(before, after))
                    paths.Add((after ?? before)!.Path);
                return;
        }
    }

    private static void AddLeaves(JContainer container, List<string> paths)
    {
        foreach (var child in container.Children())
        {
            var value = child is JProperty property ? property.Value : child;
            if (value is JContainer { HasValues: true } nested)
                AddLeaves(nested, paths);
            else
                paths.Add(value.Path);
        }
    }
}
