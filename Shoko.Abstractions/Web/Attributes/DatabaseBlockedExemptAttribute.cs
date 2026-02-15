using System;

namespace Shoko.Abstractions.Web.Attributes;

/// <summary>
///   Marks a controller or endpoint as exempt from the normal database blocking
///   which occurs during application startup until the database is ready.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
public class DatabaseBlockedExemptAttribute : Attribute { }
