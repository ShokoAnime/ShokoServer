using System;

namespace Shoko.Server.Scheduling.Acquisition.Attributes;

[AttributeUsage(AttributeTargets.Class)]
public class AniDBHttpRateLimitedAttribute : NetworkRequiredAttribute { }
