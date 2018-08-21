using System;
using System.Collections.Generic;
using System.Linq;

namespace Shoko.Server.API.MVCRouter
{
    public class ParameterType
    {
        public static List<ParameterType> InstanceTypes = new List<ParameterType>()
        {
            new ParameterType("int", "int", new[] {typeof(int), typeof(int?),typeof(uint), typeof(uint?),typeof(short), typeof(short?),typeof(ushort), typeof(ushort?),typeof(byte), typeof(byte?),typeof(ushort), typeof(ushort?)}),
            new ParameterType("long", "long", new[] {typeof(long), typeof(long?),typeof(ulong), typeof(ulong?)}),
            new ParameterType("decimal", "decimal", new[] {typeof(decimal), typeof(decimal?)}),
            new ParameterType("bool", "bool", new[] {typeof(bool), typeof(bool?)}),
            new ParameterType("guid", "guid", new[] {typeof(Guid), typeof(Guid?)}),
            new ParameterType("alpha", "alpha", new[] {typeof(string)}),
            new ParameterType("datetime", "datetime", new[] {typeof(DateTime), typeof(DateTime?)}),
            new ParameterType("min", "int",new[] {typeof(int), typeof(int?),typeof(uint), typeof(uint?),typeof(short), typeof(short?),typeof(ushort), typeof(ushort?),typeof(byte), typeof(byte?),typeof(ushort), typeof(ushort?)}),
            new ParameterType("max", "int",new[] {typeof(int), typeof(int?),typeof(uint), typeof(uint?),typeof(short), typeof(short?),typeof(ushort), typeof(ushort?),typeof(byte), typeof(byte?),typeof(ushort), typeof(ushort?)}),
            new ParameterType("range", "int",new[] {typeof(int), typeof(int?),typeof(uint), typeof(uint?),typeof(short), typeof(short?),typeof(ushort), typeof(ushort?),typeof(byte), typeof(byte?),typeof(ushort), typeof(ushort?)}),
            new ParameterType("minlength", "string", new[] {typeof(string)}),
            new ParameterType("maxlength", "string", new[] {typeof(string)}),
            new ParameterType("length", "string", new[] {typeof(string)}),
        };


        public ParameterType(string name, string basetype, IEnumerable<Type> types)
        {
            Name = name;
            BaseType = basetype;
            Types = types.ToList();
        }

        public bool Supports(Type t)
        {
            return Types.Contains(t);
        }

        public string Name { get; set; }

        public string BaseType { get; set; }
        public List<Type> Types { get; set; }


    }

    public class ParamInfo
    {
        public string Name { get; set; }
        public bool Optional { get; set; }

        public ParameterType Constraint { get; set; }
    }
}
