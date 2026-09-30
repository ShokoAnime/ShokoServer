using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using NHibernate;
using NHibernate.Dialect;
using NHibernate.Engine;
using NHibernate.SqlCommand;
using NHibernate.SqlTypes;
using NHibernate.Type;

namespace Shoko.Server.Databases;

public partial class DatabaseFixes
{
    #region Batched Updates

    // SQL Server takes at most 2,100 parameters in one statement.
    private const int MaxParametersPerStatement = 2000;

    // A negative SQLite cache size is in KiB, so this is 256 MiB.
    private const int SQLiteWriteCacheSize = -262144;

    /// <summary>
    /// Sets columns of many rows to values of their own, a chunk of rows per
    /// statement: <c>UPDATE table SET column = CASE key WHEN … THEN … END WHERE key IN (…)</c>.
    /// The columns are assigned in the order given. The statements run in the
    /// session's open transaction, and the values are bound through their
    /// NHibernate types, so each database stores them the way the mappings do.
    /// </summary>
    /// <typeparam name="TRow">What describes one row.</typeparam>
    /// <param name="session">The session, with a transaction open.</param>
    /// <param name="table">The table.</param>
    /// <param name="keyColumn">The column the rows are found by.</param>
    /// <param name="keyType">The NHibernate type of the key column.</param>
    /// <param name="rows">The rows to update.</param>
    /// <param name="getKey">Gets the key of a row.</param>
    /// <param name="columns">The columns to set, each with its NHibernate type and a way to get its value for a row.</param>
    /// <param name="advance">Called with the number of rows in each chunk once it is written, or <c>null</c>.</param>
    private static void UpdateRowsByKey<TRow>(
        IStatelessSession session,
        string table,
        string keyColumn,
        IType keyType,
        IReadOnlyList<TRow> rows,
        Func<TRow, object> getKey,
        IReadOnlyList<(string Column, IType Type, Func<TRow, object> GetValue)> columns,
        Action<int>? advance = null
    )
    {
        ExecuteInChunks(
            session,
            rows,
            2 * columns.Count + 1,
            count =>
            {
                var sql = new SqlStringBuilder().Add($"UPDATE {table} SET ");
                var types = new List<IType>();
                for (var column = 0; column < columns.Count; column++)
                {
                    if (column > 0)
                        sql.Add(", ");
                    sql.Add($"{columns[column].Column} = CASE {keyColumn}");
                    for (var row = 0; row < count; row++)
                    {
                        sql.Add(" WHEN ").AddParameter().Add(" THEN ").AddParameter();
                        types.Add(keyType);
                        types.Add(columns[column].Type);
                    }

                    sql.Add($" ELSE {columns[column].Column} END");
                }

                AddKeyList(sql, types, keyColumn, keyType, count);
                return (sql.ToSqlString(), types);
            },
            chunk => columns
                .SelectMany(column => chunk.SelectMany(row => new[] { getKey(row), column.GetValue(row) }))
                .Concat(chunk.Select(getKey)),
            advance
        );
    }

    /// <summary>
    /// Deletes rows by their keys, a chunk of keys per statement, in the
    /// session's open transaction.
    /// </summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <param name="session">The session, with a transaction open.</param>
    /// <param name="table">The table.</param>
    /// <param name="keyColumn">The column the rows are found by.</param>
    /// <param name="keyType">The NHibernate type of the key column.</param>
    /// <param name="keys">The keys of the rows to delete.</param>
    private static void DeleteRowsByKey<TKey>(IStatelessSession session, string table, string keyColumn, IType keyType, IReadOnlyList<TKey> keys)
        where TKey : notnull
    {
        ExecuteInChunks(
            session,
            keys,
            1,
            count =>
            {
                var sql = new SqlStringBuilder().Add($"DELETE FROM {table}");
                var types = new List<IType>();
                AddKeyList(sql, types, keyColumn, keyType, count);
                return (sql.ToSqlString(), types);
            },
            chunk => chunk.Cast<object>()
        );
    }

    private static void AddKeyList(SqlStringBuilder sql, List<IType> types, string keyColumn, IType keyType, int count)
    {
        sql.Add($" WHERE {keyColumn} IN (");
        for (var row = 0; row < count; row++)
        {
            if (row > 0)
                sql.Add(", ");
            sql.AddParameter();
            types.Add(keyType);
        }

        sql.Add(")");
    }

    // Reuses the prepared command while the chunk size stays the same. SQLite gets a row per
    // statement: it has no round trip, and converting the GUID text of every case branch is slow.
    private static void ExecuteInChunks<T>(
        IStatelessSession session,
        IReadOnlyList<T> items,
        int parametersPerItem,
        Func<int, (SqlString Sql, IReadOnlyList<IType> Types)> build,
        Func<T[], IEnumerable<object>> getValues,
        Action<int>? advance = null
    )
    {
        var implementor = session.GetSessionImplementation();
        var batcher = implementor.Batcher;
        DbCommand? command = null;
        IReadOnlyList<IType> types = [];
        var commandSize = 0;
        var chunkSize = implementor.Factory.Dialect is SQLiteDialect ? 1 : MaxParametersPerStatement / parametersPerItem;
        try
        {
            foreach (var chunk in items.Chunk(chunkSize))
            {
                if (command is null || commandSize != chunk.Length)
                {
                    if (command is not null)
                        batcher.CloseCommand(command, null);
                    (var sql, types) = build(chunk.Length);
                    command = batcher.PrepareCommand(CommandType.Text, sql, types.Select(type => GetSqlType(type, implementor)).ToArray());
                    commandSize = chunk.Length;
                }

                var index = 0;
                foreach (var value in getValues(chunk))
                {
                    types[index].NullSafeSet(command, value, index, implementor);
                    index++;
                }

                batcher.ExecuteNonQuery(command);
                advance?.Invoke(chunk.Length);
            }
        }
        finally
        {
            if (command is not null)
                batcher.CloseCommand(command, null);
        }
    }

    /// <summary>
    /// Runs writes in one transaction of the session and commits them. SQLite
    /// gets a larger page cache while they run, since a rewritten key moves
    /// in an index far larger than the default cache.
    /// </summary>
    /// <param name="session">The session, with no transaction open.</param>
    /// <param name="write">Makes the writes.</param>
    private static void WriteInTransaction(IStatelessSession session, Action write)
    {
        using var transaction = session.BeginTransaction();
        var cacheSize = session.GetSessionImplementation().Factory.Dialect is SQLiteDialect
            ? Convert.ToInt64(session.CreateSQLQuery("PRAGMA cache_size").UniqueResult())
            : (long?)null;
        if (cacheSize.HasValue)
            session.CreateSQLQuery($"PRAGMA cache_size = {SQLiteWriteCacheSize}").ExecuteUpdate();
        try
        {
            write();
        }
        finally
        {
            // Setting the cache back does not free its pages; shrinking does.
            if (cacheSize.HasValue)
            {
                session.CreateSQLQuery($"PRAGMA cache_size = {cacheSize.Value}").ExecuteUpdate();
                session.CreateSQLQuery("PRAGMA shrink_memory").ExecuteUpdate();
            }
        }

        transaction.Commit();
    }

    private static SqlType GetSqlType(IType type, ISessionImplementor implementor)
        => type.SqlTypes(implementor.Factory).Single();

    #endregion
}
