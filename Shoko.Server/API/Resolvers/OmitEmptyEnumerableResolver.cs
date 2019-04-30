using System.Collections;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Shoko.Server.API
{
    public class OmitEmptyEnumerableResolver : DefaultContractResolver
    {
        protected override JsonProperty CreateProperty(MemberInfo member,
            MemberSerialization memberSerialization)
        {
            var property = base.CreateProperty(member, memberSerialization);
            // strings are enumerable, but don't count them
            if (property.PropertyType == typeof(string)) return property;

            if (typeof(IEnumerable).IsAssignableFrom(property.PropertyType))
            {
                property.ShouldSerialize = instance => EnumerableAny(member, instance);
            }

            return property;
        }

        private static bool EnumerableAny(MemberInfo member, object instance)
        {
            IEnumerable enumerable = null;
            // this value could be in a public field or public property
            switch (member.MemberType)
            {
                case MemberTypes.Property:
                    enumerable = instance
                        .GetType()
                        .GetProperty(member.Name)
                        ?.GetValue(instance, null) as IEnumerable;
                    break;
                case MemberTypes.Field:
                    enumerable = instance
                        .GetType()
                        .GetField(member.Name)
                        .GetValue(instance) as IEnumerable;
                    break;
            }

            return enumerable == null || enumerable.GetEnumerator().MoveNext();
            // if the list is null, we defer the decision to NullValueHandling
        }
    }
}