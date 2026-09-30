using System;

namespace Shoko.Server.API.Annotations;

/// <summary>
///   Marks a request body field taking a <see cref="Abstractions.Metadata.MetadataSource"/>
///   or <see cref="Abstractions.Metadata.MetadataEntityType"/>, or a
///   collection of them, so the OpenAPI document lists the registered values
///   for it as a string enum.
/// </summary>
/// <remarks>
///   Only request fields are marked. The shared schemas of the two types also
///   describe response fields, which send the old spellings, so they stay
///   free strings.
/// </remarks>
[AttributeUsage(AttributeTargets.Property, Inherited = true)]
public sealed class RegisteredMetadataValuesAttribute : Attribute;
