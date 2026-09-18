using System;

namespace Shoko.Server.API.Annotations;

/// <summary>
///   Marks an endpoint that accepts authentication but doesn't always require
///   it. Use it when the response depends on whether, and as whom, the caller
///   is authenticated, or when a setting decides whether anonymous callers are
///   let through. The OpenAPI document then lists the API key as optional, so
///   clients know to send it when they have one.
/// </summary>
/// <remarks>
///   Has no effect on an endpoint that requires authorization, i.e. one with
///   an <see cref="Microsoft.AspNetCore.Authorization.AuthorizeAttribute"/>
///   and no <see cref="Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute"/>.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class OptionalAuthenticationAttribute : Attribute;
