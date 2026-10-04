using System;
using System.Data;
using Microsoft.Data.Sqlite;
using Moq;
using NHibernate;
using NHibernate.Engine;
using Shoko.Server.Databases.NHibernate;
using Xunit;

namespace Shoko.Tests.Databases;

public class PooledStringTypeTests
{
    #region Reading

    [Fact]
    public void RowsReadShareOneInstanceOfEachValue()
    {
        using var reader = Rows("image/jpeg", "image/png", "image/jpeg", null);
        var type = new PooledStringType();
        var values = new object?[4];

        for (var i = 0; reader.Read(); i++)
            values[i] = type.NullSafeGet(reader, ["Value"], null!, null!);

        Assert.Equal(["image/jpeg", "image/png", "image/jpeg", null], values);
        Assert.Same(values[0], values[2]);
    }

    [Fact]
    public void ReadsTheSameValuesAsTheDefaultStringType()
    {
        using var pooledReader = Rows("ja", "", "x-jat", null);
        using var defaultReader = Rows("ja", "", "x-jat", null);
        var type = new PooledStringType();

        while (pooledReader.Read() && defaultReader.Read())
        {
            Assert.Equal(
                NHibernateUtil.String.NullSafeGet(defaultReader, "Value", null!, null!),
                type.NullSafeGet(pooledReader, ["Value"], null!, null!)
            );
        }
    }

    #endregion

    #region Writing

    [Theory]
    [InlineData("image/jpeg")]
    [InlineData("")]
    [InlineData(null)]
    public void WritesTheSameParameterAsTheDefaultStringType(string? value)
    {
        using var pooled = Command();
        using var plain = Command();

        var session = new Mock<ISessionImplementor> { DefaultValue = DefaultValue.Mock }.Object;

        new PooledStringType().NullSafeSet(pooled, value, 0, session);
        NHibernateUtil.String.NullSafeSet(plain, value, 0, session);

        Assert.Equal(plain.Parameters[0].Value, pooled.Parameters[0].Value);
        Assert.Equal(plain.Parameters[0].DbType, pooled.Parameters[0].DbType);
    }

    [Fact]
    public void DeclaresTheSameColumnTypeAsTheDefaultStringType()
        => Assert.Equal(NHibernateUtil.String.SqlType, Assert.Single(new PooledStringType().SqlTypes));

    #endregion

    #region Helpers

    private static DataTableReader Rows(params string?[] values)
    {
        var table = new DataTable();
        table.Columns.Add("Value", typeof(string));
        foreach (var value in values)
            table.Rows.Add(value is null ? DBNull.Value : new string(value.AsSpan()));

        return table.CreateDataReader();
    }

    private static SqliteCommand Command()
    {
        var command = new SqliteCommand();
        command.Parameters.Add(new SqliteParameter { ParameterName = "@p0", DbType = DbType.String });
        return command;
    }

    #endregion
}
