using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Shoko.Server.Services.Configuration;

/// <summary>
///   Legacy compatibility, to be removed: rewrites stored
///   <see cref="Abstractions.UI.Components.SelectComponent{TValue}"/> values
///   written before #1414 dropped their explicit JSON names, so they pass the
///   schema validation that runs ahead of deserialization.
/// </summary>
internal static class LegacySelectComponentMigration
{
    private static readonly Dictionary<string, string> ComponentKeys = new()
    {
        ["options"] = "Options",
        ["groups"] = "Groups",
    };

    private static readonly Dictionary<string, string> OptionKeys = new()
    {
        ["label"] = "Label",
        ["groupId"] = "GroupID",
        ["value"] = "Value",
        ["selected"] = "IsSelected",
        ["default"] = "IsDefault",
        ["disabled"] = "IsDisabled",
    };

    private static readonly Dictionary<string, string> GroupKeys = new()
    {
        ["id"] = "ID",
        ["label"] = "Label",
        ["disabled"] = "IsDisabled",
    };

    /// <summary>
    ///   Renames the old keys of every select value in the document.
    /// </summary>
    /// <param name="json">The stored configuration.</param>
    /// <returns>
    ///   The rewritten document, or <paramref name="json"/> itself if nothing
    ///   needed rewriting.
    /// </returns>
    public static string Apply(string json)
    {
        var root = JObject.Parse(json);
        var changed = false;
        foreach (var obj in root.DescendantsAndSelf().OfType<JObject>().Where(IsLegacySelect).ToList())
        {
            Rename(obj, ComponentKeys);
            foreach (var option in ((JArray)obj["Options"]!).OfType<JObject>())
                Rename(option, OptionKeys);
            foreach (var group in ((JArray)obj["Groups"]!).OfType<JObject>())
                Rename(group, GroupKeys);
            changed = true;
        }

        return changed ? root.ToString(Formatting.Indented) : json;
    }

    // Both arrays plus a "value" on every option keeps unrelated objects that merely have an "options" key out.
    private static bool IsLegacySelect(JObject obj)
        => obj["options"] is JArray options && obj["groups"] is JArray
            && options.All(o => o is JObject option && option.ContainsKey("value"));

    private static void Rename(JObject obj, Dictionary<string, string> keys)
    {
        foreach (var (oldKey, newKey) in keys)
        {
            if (!obj.TryGetValue(oldKey, out var value))
                continue;

            obj.Remove(oldKey);
            if (!obj.ContainsKey(newKey))
                obj[newKey] = value;
        }
    }
}
