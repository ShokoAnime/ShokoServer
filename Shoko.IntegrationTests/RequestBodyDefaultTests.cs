using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using Shoko.Abstractions.User.Enums;
using Shoko.Server.API.v3.Controllers;
using Shoko.Server.API.v3.Models.Shoko;
using Xunit;

namespace Shoko.IntegrationTests;

/// <summary>
/// Reads request bodies with the started server's own JSON settings, and
/// checks that each omitted member of every APIv3 body type keeps the value
/// its initializer gives it, without a hand-written setter being called for it.
/// </summary>
/// <remarks>
/// The API reads bodies with <see cref="DefaultValueHandling.Populate"/>,
/// which sets an omitted member to its <c>[DefaultValue]</c>, or else to the
/// type's default, over the initializer.
/// </remarks>
[Collection(DatabaseCollection.Name)]
public class RequestBodyDefaultTests(DatabaseMigrationFixture fixture)
{
    #region Tests

    [Fact]
    public void EmptyBody_KeepsEveryInitializer()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);
        var services = fixture.Services;
        var settings = services.GetRequiredService<IOptions<MvcNewtonsoftJsonOptions>>().Value.SerializerSettings;
        var types = GetBodyTypes(services);
        Assert.NotEmpty(types);

        var offenders = new List<string>();
        foreach (var type in types.OrderBy(Name))
            offenders.AddRange(Check(type, settings));

        Assert.True(offenders.Count == 0, $"{offenders.Count} omitted members lose their initializer:\n{string.Join('\n', offenders)}");
    }

    [Fact]
    public void HandWrittenSetters_AreNotPopulated()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);
        var services = fixture.Services;
        var settings = services.GetRequiredService<IOptions<MvcNewtonsoftJsonOptions>>().Value.SerializerSettings;

        // Populating calls the setter of each omitted member, so a setter that tracks what was sent
        // or changes another member would act on members the client never sent.
        var offenders = new List<string>();
        foreach (var type in GetBodyTypes(services).OrderBy(Name))
        {
            if (settings.ContractResolver!.ResolveContract(type) is not JsonObjectContract contract)
                continue;

            foreach (var property in contract.Properties)
            {
                if (!property.Writable || property.Ignored || type.GetProperty(property.UnderlyingName!, BindingFlags.Public | BindingFlags.Instance) is not { } member)
                    continue;

                if (member.SetMethod is null || member.SetMethod.IsDefined(typeof(CompilerGeneratedAttribute)))
                    continue;

                if ((property.DefaultValueHandling ?? settings.DefaultValueHandling).HasFlag(DefaultValueHandling.Populate))
                    offenders.Add($"{Name(type)}.{property.UnderlyingName}");
            }
        }

        Assert.True(offenders.Count == 0, $"{offenders.Count} hand-written setters are called for omitted members:\n{string.Join('\n', offenders)}");
    }

    [Fact]
    public void SeriesUserData_OneRatingMember_IsKept()
    {
        var rating = Read<Series.SeriesUserData>("""{"UserRating": 7}""");
        var voteType = Read<Series.SeriesUserData>("""{"UserRatingVoteType": "Permanent"}""");

        Assert.Equal((7.0, SeriesVoteType.Temporary), (rating.UserRating, rating.UserRatingVoteType));
        Assert.Equal((1.0, SeriesVoteType.Permanent), (voteType.UserRating, voteType.UserRatingVoteType));
    }

#if DEBUG
    [Fact]
    public void AnidbUdpRequest_OmittedFullResponse_FollowsTheAction()
    {
        var login = Read<DebugController.AnidbUdpRequest>("""{"Action": "LOGIN"}""");
        var ping = Read<DebugController.AnidbUdpRequest>("""{"Action": "PING"}""");
        var sent = Read<DebugController.AnidbUdpRequest>("""{"Action": "LOGIN", "FullResponse": false}""");

        Assert.Equal((true, false, false), (login.FullResponse, ping.FullResponse, sent.FullResponse));
    }
#endif

    #endregion

    #region Harness

    /// <summary>
    /// Reads a body with the API's serializer settings.
    /// </summary>
    /// <typeparam name="T">The body type.</typeparam>
    /// <param name="json">The request body.</param>
    /// <returns>The body read.</returns>
    private T Read<T>(string json)
    {
        Assert.True(fixture.Success, fixture.FailureMessage);
        var settings = fixture.Services.GetRequiredService<IOptions<MvcNewtonsoftJsonOptions>>().Value.SerializerSettings;
        return Assert.IsType<T>(JsonSerializer.Create(settings).Deserialize(new StringReader(json), typeof(T)));
    }

    /// <summary>
    /// Compares a new instance of a type with one read from <c>{}</c>.
    /// </summary>
    /// <param name="type">The type to check.</param>
    /// <param name="settings">The API's serializer settings.</param>
    /// <returns>A line for each member that lost its initializer.</returns>
    private static IEnumerable<string> Check(Type type, JsonSerializerSettings settings)
    {
        if (settings.ContractResolver!.ResolveContract(type) is not JsonObjectContract contract || contract.DefaultCreator is null || contract.DefaultCreatorNonPublic)
            yield break;

        var serializer = JsonSerializer.Create(settings);
        serializer.Error += (_, args) => args.ErrorContext.Handled = true;
        var expected = contract.DefaultCreator();
        var actual = serializer.Deserialize(new StringReader("{}"), type);
        if (actual is null)
        {
            yield return $"{Name(type)}: could not be read from {{}}";
            yield break;
        }

        foreach (var property in contract.Properties)
        {
            if (!property.Writable || property.Ignored || property.ValueProvider is null || IsRequired(type, property))
                continue;

            var expectedValue = property.ValueProvider.GetValue(expected);
            var actualValue = property.ValueProvider.GetValue(actual);
            if (!AreEqual(expectedValue, actualValue))
                yield return $"{Name(type)}.{property.UnderlyingName}: {Show(expectedValue)} -> {Show(actualValue)}";
        }
    }

    /// <summary>
    /// Checks whether a missing member is a validation error rather than a
    /// default. A value type can't be missing once read, so it never is.
    /// </summary>
    /// <param name="type">The declaring type.</param>
    /// <param name="property">The member.</param>
    /// <returns><c>true</c> if the member is required and can be null.</returns>
    private static bool IsRequired(Type type, JsonProperty property)
    {
        if (property.PropertyType is { IsValueType: true } propertyType && Nullable.GetUnderlyingType(propertyType) is null)
            return false;

        if (property.Required is not Required.Default)
            return true;

        var member = (MemberInfo?)type.GetProperty(property.UnderlyingName!, BindingFlags.Public | BindingFlags.Instance)
            ?? type.GetField(property.UnderlyingName!, BindingFlags.Public | BindingFlags.Instance);
        return member is not null && (member.IsDefined(typeof(RequiredAttribute), true) || member.IsDefined(typeof(RequiredMemberAttribute), true));
    }

    /// <summary>
    /// Compares two member values by their plain JSON form.
    /// </summary>
    /// <param name="expected">The initializer's value.</param>
    /// <param name="actual">The value read.</param>
    /// <returns><c>true</c> if they are the same.</returns>
    private static bool AreEqual(object? expected, object? actual)
    {
        if (expected is null || actual is null)
            return expected is null && actual is null;

        // An initializer taking the current time runs again for each instance.
        if (expected is DateTime expectedDate && actual is DateTime actualDate)
            return (expectedDate - actualDate).Duration() < TimeSpan.FromMinutes(1);

        if (expected is DateTimeOffset expectedOffset && actual is DateTimeOffset actualOffset)
            return (expectedOffset - actualOffset).Duration() < TimeSpan.FromMinutes(1);

        try
        {
            return JToken.DeepEquals(JToken.FromObject(expected), JToken.FromObject(actual));
        }
        catch (JsonException)
        {
            return Equals(expected, actual);
        }
    }

    private static string Show(object? value)
    {
        if (value is null)
            return "null";

        if (value is string text)
            return $"\"{text}\"";

        if (value is ICollection collection)
            return $"{value.GetType().Name}[{collection.Count}]";

        return value.ToString() ?? value.GetType().Name;
    }

    private static string Name(Type type)
    {
        var name = type.IsGenericType ? type.Name[..type.Name.IndexOf('`')] : type.Name;
        if (type.IsGenericType)
            name += $"<{string.Join(", ", type.GetGenericArguments().Select(Name))}>";

        return type.DeclaringType is { } declaringType ? $"{Name(declaringType)}.{name}" : name;
    }

    /// <summary>
    /// Gathers every type reachable from the body of an APIv3 action.
    /// </summary>
    /// <param name="services">The started server's services.</param>
    /// <returns>The types, excluding framework types.</returns>
    private static HashSet<Type> GetBodyTypes(IServiceProvider services)
    {
        var actions = services.GetRequiredService<IActionDescriptorCollectionProvider>().ActionDescriptors.Items
            .OfType<ControllerActionDescriptor>()
            .Where(action => action.ControllerTypeInfo.Namespace?.StartsWith("Shoko.Server.API.v3") ?? false);
        var types = new HashSet<Type>();
        var queue = new Queue<Type>();
        foreach (var action in actions)
        {
            foreach (var parameter in action.Parameters)
            {
                if (parameter.BindingInfo?.BindingSource == BindingSource.Body)
                    queue.Enqueue(parameter.ParameterType);
            }
        }

        while (queue.TryDequeue(out var type))
        {
            if (Nullable.GetUnderlyingType(type) is { } underlyingType)
                type = underlyingType;

            if (type.IsArray)
            {
                queue.Enqueue(type.GetElementType()!);
                continue;
            }

            // A patch body holds operations, applied to an instance the
            // server already has, not a new instance of its target.
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(JsonPatchDocument<>))
                continue;

            if (type.IsGenericType)
            {
                foreach (var argument in type.GetGenericArguments())
                    queue.Enqueue(argument);
            }

            if (type.IsPrimitive || type.IsEnum || type.IsInterface || type.IsAbstract || !(type.Assembly.GetName().Name?.StartsWith("Shoko.") ?? false))
                continue;

            if (!types.Add(type))
                continue;

            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                queue.Enqueue(property.PropertyType);
        }

        return types;
    }

    #endregion
}
