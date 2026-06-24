using System;
using Asp.Versioning;

namespace Shoko.Server.API.Annotations;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
public sealed class ApiV3Attribute : ApiVersionAttribute
{
    public ApiV3Attribute() : base(new ApiVersion(3, 0))
    {
    }
}
