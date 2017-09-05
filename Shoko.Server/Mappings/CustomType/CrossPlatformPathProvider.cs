using NHibernate.UserTypes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NHibernate.SqlTypes;
using System.Data;
using System.Diagnostics;
using NHibernate;

namespace Shoko.Server.Mappings.CustomType
{
    class CrossPlatformPathProvider : IUserType
    {
        public SqlType[] SqlTypes => new [] { SqlTypeFactory.GetString(255) };

        public Type ReturnedType => typeof(string);

        public bool IsMutable => false;

        public object Assemble(object cached, object owner) => cached;

        public object DeepCopy(object value) => value;

        public object Disassemble(object value) => value;

        public new bool Equals(object x, object y) => object.Equals(x, y);

        public int GetHashCode(object x) => x?.GetHashCode() ?? 0;

        public object NullSafeGet(IDataReader rs, string[] names, object owner)
        {
            object obj = NHibernateUtil.UInt32.NullSafeGet(rs, names[0]);
            if (obj == null)
                return null;

            JsonStructure structure = Newtonsoft.Json.JsonConvert.DeserializeObject<JsonStructure>((string)obj);

            int p = (int)structure.OS;
            if ((p == 4) || (p == 6) || (p == 128))
            {
                //linux
                switch (structure.Type)
                {
                    case PathType.Absolute:
                        return $"{System.IO.Path.DirectorySeparatorChar}{string.Join($"{System.IO.Path.DirectorySeparatorChar}", structure.Parts)}";
                    case PathType.Relative:
                    default:
                        return string.Join($"{System.IO.Path.DirectorySeparatorChar}", structure.Parts);
                }
            }
            return string.Join($"{System.IO.Path.DirectorySeparatorChar}", structure.Parts);
        }

        public void NullSafeSet(IDbCommand cmd, object value, int index)
        {
            if (value == null)
                ((IDataParameter)cmd.Parameters[index]).Value = DBNull.Value;
            else
            {
                string path = (string)value;
                JsonStructure structure = new JsonStructure()
                {
                    Type = System.IO.Path.IsPathRooted(path) ? PathType.Absolute : PathType.Relative,
                    Parts = path.Split('\\'),
                    OS = Environment.OSVersion.Platform
                };

                ((IDataParameter)cmd.Parameters[index]).Value = Newtonsoft.Json.JsonConvert.SerializeObject(structure);
            }
                
        }

        public object Replace(object original, object target, object owner) => original;

        internal class JsonStructure
        {
            public PlatformID OS { get; set; }
            public PathType Type { get; set; } = PathType.Relative;
            public string[] Parts { get; set; }
        }
        internal enum PathType
        {
            /// <summary>
            /// From the root
            /// </summary>
            Absolute,

            /// <summary>
            /// This is from the current directory
            /// </summary>
            Relative
        }
    }
}
