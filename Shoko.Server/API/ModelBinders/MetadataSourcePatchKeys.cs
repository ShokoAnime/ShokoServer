using System;
using System.Linq;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Newtonsoft.Json.Serialization;
using Shoko.Abstractions.Metadata;

namespace Shoko.Server.API.ModelBinders;

/// <summary>
///   Checks the dictionary keys in a JSON Patch document's paths that stand
///   for a <see cref="MetadataSource"/> or a <see cref="MetadataEntityType"/>.
///   JSON Patch reads such a key with the type's own converter, which makes
///   and keeps one for any valid text, so a patch is checked before it is
///   applied.
/// </summary>
public static class MetadataSourcePatchKeys
{
    /// <summary>
    ///   Checks that every key in the patch's paths that stands for a source
    ///   or an entity type names a registered one, by its value or an alias,
    ///   ignoring case.
    /// </summary>
    /// <typeparam name="T">The patched type.</typeparam>
    /// <param name="document">The patch.</param>
    /// <param name="modelState">Where to add an error for each key refused.</param>
    /// <returns><c>true</c> if every such key names a registered source or entity type.</returns>
    public static bool Validate<T>(JsonPatchDocument<T> document, ModelStateDictionary modelState) where T : class
    {
        var valid = true;
        foreach (var operation in document.Operations)
        {
            foreach (var path in new[] { operation.path, operation.from })
            {
                if (string.IsNullOrEmpty(path) || FindUnregisteredKey(typeof(T), path, document.ContractResolver) is not { } found)
                    continue;

                modelState.TryAddModelError(typeof(T).Name, $"\"{found.Key}\" in the path \"{path}\" is not a registered metadata {found.Kind}.");
                valid = false;
            }
        }

        return valid;
    }

    // Follows the path through the contracts, as JSON Patch does, and gives the first key that names no registered source or entity type.
    private static (string Key, string Kind)? FindUnregisteredKey(Type type, string path, IContractResolver resolver)
    {
        var current = type;
        foreach (var segment in path.Split('/').Skip(1).Select(segment => segment.Replace("~1", "/").Replace("~0", "~")))
        {
            switch (resolver.ResolveContract(current))
            {
                case JsonDictionaryContract dictionary:
                    if (dictionary.DictionaryKeyType == typeof(MetadataSource) && !MetadataSource.TryGet(segment, out _))
                        return (segment, "source");
                    if (dictionary.DictionaryKeyType == typeof(MetadataEntityType) && !MetadataEntityType.TryGet(segment, out _))
                        return (segment, "entity type");

                    current = dictionary.DictionaryValueType;
                    break;
                case JsonArrayContract array:
                    current = array.CollectionItemType;
                    break;
                case JsonObjectContract objectContract:
                    current = objectContract.Properties.GetClosestMatchProperty(segment)?.PropertyType;
                    break;
                default:
                    return null;
            }

            if (current is null)
                return null;
        }

        return null;
    }
}
