using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Serialization;
using Shoko.Abstractions.Actions;

namespace Shoko.Server.Services.Configuration;

/// <summary>
///   Hides an executable action's own metadata surface from the schema
///   generator, so only the action's invocation parameters are described.
/// </summary>
/// <remarks>
///   <para>
///     A configuration is described by every settable, serialized property it
///     has, and an action's parameters work the same way — the caller's payload
///     is populated straight onto the action instance. An action instance also
///     carries its metadata as ordinary public properties though, and the
///     configuration rule would sweep <c>Name</c>, <c>Description</c>,
///     <c>Category</c>, <c>Permission</c>, <c>RequiresConfirmation</c>,
///     <c>ConfirmationMessage</c> and <c>Scope</c> in as if the caller were
///     meant to supply them.
///   </para>
///   <para>
///     The excluded set is derived by reflection over
///     <see cref="IExecutableAction"/> and the four scoped base classes rather
///     than written out, so it cannot drift from the contract, and a plugin
///     author never has to annotate a parameter to opt it in.
///   </para>
///   <para>
///     Both the name and the type have to match before a property is dropped,
///     so an action whose parameter merely happens to be called <c>Name</c>
///     keeps it as long as it is not the <see cref="IExecutableAction.Name"/>
///     implementation itself.
///   </para>
///   <para>
///     The scoped context (<c>SeriesAction.Series</c> and friends) needs no
///     entry: it is a <see langword="protected"/> property, and Newtonsoft only
///     considers public members, so it never reaches the generator in the first
///     place. <c>IScopedAction.SetContext</c> is likewise both a method and an
///     explicit implementation of an interface this assembly cannot even name.
///   </para>
/// </remarks>
internal sealed class ActionMetadataContractResolver : DefaultContractResolver
{
    /// <summary>
    ///   The scoped base classes, which add <c>Scope</c> on top of what
    ///   <see cref="IExecutableAction"/> declares.
    /// </summary>
    private static readonly Type[] _scopedBaseTypes = [typeof(SeriesAction), typeof(GroupAction), typeof(EpisodeAction), typeof(VideoAction)];

    /// <summary>
    ///   The metadata surface, as property name to declared type.
    /// </summary>
    internal static readonly IReadOnlyDictionary<string, Type> MetadataMembers = typeof(IExecutableAction)
        .GetProperties()
        .Concat(_scopedBaseTypes.SelectMany(x => x.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)))
        .GroupBy(x => x.Name, StringComparer.Ordinal)
        .ToDictionary(x => x.Key, x => x.First().PropertyType, StringComparer.Ordinal);

    /// <inheritdoc />
    protected override List<MemberInfo> GetSerializableMembers(Type objectType)
    {
        var members = base.GetSerializableMembers(objectType);

        // Only the action itself carries the metadata surface; a parameter's own
        // type is walked by the ordinary configuration rules, so a nested class
        // with a `Name` on it keeps it.
        if (!objectType.IsAssignableTo(typeof(IExecutableAction)))
            return members;

        return members.Where(x => !IsMetadataMember(x)).ToList();
    }

    private static bool IsMetadataMember(MemberInfo member)
        => member is PropertyInfo property &&
            MetadataMembers.TryGetValue(property.Name, out var declaredType) &&
            property.PropertyType == declaredType;
}
