using System;

namespace Shoko.Server.API.Annotations
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
    public class InitFriendlyAttribute : Attribute
    {
    }
}