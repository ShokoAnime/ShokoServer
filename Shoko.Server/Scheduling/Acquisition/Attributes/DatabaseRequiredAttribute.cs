using System;

namespace Shoko.Server.Scheduling.Acquisition.Attributes;

/// <summary>Marks a job as requiring the database to be available (not blocked/initializing).</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = true)]
public sealed class DatabaseRequiredAttribute : Attribute { }
