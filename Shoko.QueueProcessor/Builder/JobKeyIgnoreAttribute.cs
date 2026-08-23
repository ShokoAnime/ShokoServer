using System;

namespace Shoko.QueueProcessor.Builder;

/// <summary>
/// Excludes a property from the unique job key. Only has an effect when the job
/// declares no <see cref="JobKeyMemberAttribute"/> members at all — in that case
/// the key is built from every eligible primitive property, and this opts one
/// back out. Use it for job data that varies between callers but must not make
/// two otherwise-identical jobs queue alongside each other.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class JobKeyIgnoreAttribute : Attribute;
