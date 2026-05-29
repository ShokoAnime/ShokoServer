using System;

namespace Shoko.QueueProcessor.Acquisition.Attributes;

/// <summary>Marks a job as requiring the database to be available (not blocked/initializing).</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = true)]
public sealed class DatabaseRequiredAttribute : Attribute { }
