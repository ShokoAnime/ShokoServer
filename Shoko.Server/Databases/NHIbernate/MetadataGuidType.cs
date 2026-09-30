using System;
using System.Data;
using System.Data.Common;
using NHibernate;
using NHibernate.Engine;
using NHibernate.SqlTypes;
using NHibernate.UserTypes;
using Shoko.Abstractions.Metadata;

namespace Shoko.Server.Databases.NHibernate;

/// <summary>
/// Stores a <see cref="MetadataGuid"/> as its text form, e.g.
/// <c>tmdb://ordering/5a8f…</c>. Text that does not parse reads back as
/// <c>null</c>.
/// </summary>
public class MetadataGuidType : IUserType
{
    /// <inheritdoc/>
    public SqlType[] SqlTypes => [NHibernateUtil.String.SqlType];

    /// <inheritdoc/>
    public Type ReturnedType => typeof(MetadataGuid);

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
    {
        var index = rs.GetOrdinal(names[0]);
        return !rs.IsDBNull(index) && MetadataGuid.TryParse(Convert.ToString(rs.GetValue(index)), out var guid) ? guid : null;
    }

    /// <inheritdoc/>
    public void NullSafeSet(DbCommand cmd, object? value, int index, ISessionImplementor session)
        => ((IDataParameter)cmd.Parameters[index]).Value = value is MetadataGuid guid ? guid.ToString() : DBNull.Value;

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
