using System;
using Shoko.QueueProcessor.Acquisition.Attributes;

namespace Shoko.Server.Scheduling.Acquisition.Attributes;

/// <summary>
/// Marks a generic job that runs for one metadata provider, its type argument, so the job is held
/// back while that provider reports it is paused. Inherits from
/// <see cref="NetworkRequiredAttribute"/>, since every such job talks to the provider's source.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class MetadataProviderJobAttribute() : NetworkRequiredAttribute;
