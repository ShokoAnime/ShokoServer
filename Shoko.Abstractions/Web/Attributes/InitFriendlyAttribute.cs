using System;

namespace Shoko.Abstractions.Web.Attributes;

/// <summary>
///   Marks a controller or endpoint as application startup friendly, meaning it
///   can be called before the server and database are ready.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
public class InitFriendlyAttribute : Attribute { }
