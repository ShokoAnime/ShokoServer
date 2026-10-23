using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Shoko.Abstractions.Filtering.Expressions;
using Shoko.Abstractions.Filtering.Expressions.Containers;
using Shoko.Abstractions.Filtering.Sorting;

namespace Shoko.Server.Databases.NHibernate;

/// <summary>
/// Stores a filter expression by its container members only: <c>Left</c>,
/// <c>Right</c>, <c>Parameter</c> and <c>SecondParameter</c>, each as the
/// container interface exposes it, whatever the class names the property
/// behind it. A sorting expression also keeps <c>Descending</c> and
/// <c>Next</c>.
/// </summary>
internal sealed class FilterExpressionContractResolver : DefaultContractResolver
{
    /// <summary>
    /// The one instance, so its contracts are built once.
    /// </summary>
    public static readonly FilterExpressionContractResolver Instance = new();

    /// <summary>
    /// The stored members in the order they are written.
    /// </summary>
    private static readonly string[] _order =
    [
        nameof(IWithExpressionParameter.Left),
        nameof(IWithSecondExpressionParameter.Right),
        nameof(IWithStringParameter.Parameter),
        nameof(IWithSecondStringParameter.SecondParameter),
        nameof(SortingExpression.Descending),
        nameof(SortingExpression.Next),
    ];

    /// <summary>
    /// The state of a sorting expression that is not a container value.
    /// </summary>
    private static readonly string[] _sortingMembers = [nameof(SortingExpression.Descending), nameof(SortingExpression.Next)];

    private FilterExpressionContractResolver() { }

    /// <summary>
    /// Lists what is stored of a type: for a filter expression its container
    /// members and, for a sorting one, its place in the chain; for anything
    /// else what Newtonsoft stores by default.
    /// </summary>
    /// <param name="type">The type being stored.</param>
    /// <param name="memberSerialization">How its members are picked.</param>
    /// <returns>The members stored, in the order they are written.</returns>
    protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
    {
        if (!typeof(FilterExpression).IsAssignableFrom(type))
            return base.CreateProperties(type, memberSerialization);

        var properties = new List<JsonProperty>();
        foreach (var container in type.GetInterfaces().Where(IsContainer).OrderBy(container => container.Name, StringComparer.Ordinal))
        {
            foreach (var member in container.GetProperties())
            {
                if (properties.Any(property => property.PropertyName == member.Name))
                    continue;

                // A public property of the same name and type is the member itself; otherwise
                // the class implements it explicitly and it is read and written through the interface.
                var publicProperty = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .FirstOrDefault(property => property.Name == member.Name && property.PropertyType == member.PropertyType);
                var property = CreateProperty((MemberInfo?)publicProperty ?? member, memberSerialization);
                property.PropertyName = member.Name;
                property.Readable = true;
                property.Writable = true;
                property.Ignored = false;
                properties.Add(property);
            }
        }

        if (typeof(SortingExpression).IsAssignableFrom(type))
            properties.AddRange(base.CreateProperties(type, memberSerialization).Where(property => _sortingMembers.Contains(property.PropertyName)));

        foreach (var property in properties)
            property.Order = Array.IndexOf(_order, property.PropertyName);

        return properties.OrderBy(property => property.Order).ToList();
    }

    /// <summary>
    /// Whether an interface is one of the filter expression containers.
    /// </summary>
    /// <param name="type">The interface.</param>
    /// <returns><c>true</c> for a container.</returns>
    private static bool IsContainer(Type type)
        => type.Namespace == typeof(IWithBoolParameter).Namespace && type.Assembly == typeof(IWithBoolParameter).Assembly;
}
