using System;
using System.Data.Common;
using NHibernate;
using NHibernate.Engine;
using NHibernate.SqlTypes;
using NHibernate.UserTypes;
using Shoko.Server.Utilities;

namespace Shoko.Server.Databases.NHibernate;

/// <summary>
///   Maps a text column as the default string type does, but hands back the
///   <see cref="StringPool"/> instance of each value read, for columns whose
///   values repeat across many rows.
/// </summary>
public class PooledStringType : IUserType
{
    /// <inheritdoc/>
    public SqlType[] SqlTypes => [NHibernateUtil.String.SqlType];

    /// <inheritdoc/>
    public Type ReturnedType => typeof(string);

    /// <inheritdoc/>
    public bool IsMutable => false;

    /// <inheritdoc/>
    public new bool Equals(object? x, object? y)
        => ReferenceEquals(x, y) || (x is not null && x.Equals(y));

    /// <inheritdoc/>
    public int GetHashCode(object x)
        => x.GetHashCode();

    /// <inheritdoc/>
    public object? NullSafeGet(DbDataReader rs, string[] names, ISessionImplementor session, object owner)
        => StringPool.Get((string?)NHibernateUtil.String.NullSafeGet(rs, names[0], session, owner));

    /// <inheritdoc/>
    public void NullSafeSet(DbCommand cmd, object? value, int index, ISessionImplementor session)
        => NHibernateUtil.String.NullSafeSet(cmd, value, index, session);

    /// <inheritdoc/>
    public object? DeepCopy(object? value)
        => value;

    /// <inheritdoc/>
    public object? Replace(object? original, object? target, object? owner)
        => original;

    /// <inheritdoc/>
    public object? Assemble(object? cached, object? owner)
        => cached;

    /// <inheritdoc/>
    public object? Disassemble(object? value)
        => value;
}
