using System;

namespace Shoko.Server.Scheduling.Acquisition.Attributes;

/// <summary>Marks a job as requiring internet connectivity.</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = true)]
public sealed class NetworkRequiredAttribute : Attribute { }
