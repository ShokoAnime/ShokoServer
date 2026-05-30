using System;

namespace Shoko.QueueProcessor.Acquisition.Attributes;

/// <summary>Marks a job as requiring the database to be available (not blocked/initializing).</summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class DatabaseRequiredAttribute : AcquisitionAttribute;
