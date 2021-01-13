using System;

namespace Shoko.Plugin.Abstractions.Attributes
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class RenamerProviderAttribute : Attribute
    {
        public Type ProviderType { get; set; }

        public RenamerProviderAttribute(Type providerType)
        {
            ProviderType = providerType;
        }
    }
}
