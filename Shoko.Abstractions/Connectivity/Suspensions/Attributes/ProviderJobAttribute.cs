using System;

namespace Shoko.Abstractions.Connectivity.Suspensions.Attributes;

/// <summary>
///   Marks a queue job that runs for one provider, so the job is held back
///   while an <see cref="Suspensions.ISuspensionProvider"/> holding that provider is
///   suspended.
/// </summary>
/// <remarks>
///   The provider is the job's single generic type argument (on the job type
///   or one of its base types), or the <c>TProvider</c> of the
///   <c>IVideoReleaseProviderJob&lt;TProvider&gt;</c> it implements. The
///   attribute is inherited and does not require the network by itself.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = true)]
public sealed class ProviderJobAttribute : Attribute;
