using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using Xunit;

namespace Shoko.IntegrationTests;

/// <summary>
/// Plain SQL against the migrated database, for the tests that seed or read rows the
/// repositories do not reach.
/// </summary>
internal static class Sql
{
    #region Public Methods

    /// <summary>
    /// Runs a statement.
    /// </summary>
    /// <param name="connection">The open connection.</param>
    /// <param name="sql">The statement.</param>
    /// <param name="parameters">The statement's parameters, a <c>null</c> value sent as <see cref="DBNull"/>.</param>
    public static void Execute(IDbConnection connection, string sql, params (string Name, object? Value)[] parameters)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }

        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Inserts one row.
    /// </summary>
    /// <param name="connection">The open connection.</param>
    /// <param name="table">The table.</param>
    /// <param name="values">The row's columns and values, a flag sent as <c>1</c> or <c>0</c>.</param>
    public static void Insert(IDbConnection connection, string table, params (string Column, object? Value)[] values)
        => Execute(
            connection,
            $"INSERT INTO {table} ({string.Join(", ", values.Select(value => value.Column))}) VALUES ({string.Join(", ", values.Select((_, index) => $"@p{index}"))})",
            [.. values.Select((value, index) => ($"@p{index}", value.Value is bool flag ? (flag ? 1 : 0) : value.Value))]
        );

    /// <summary>
    /// Reads every row of a query as one line of text.
    /// </summary>
    /// <param name="connection">The open connection.</param>
    /// <param name="sql">The query.</param>
    /// <returns>
    /// Each row's values joined by <c>|</c>, a <c>null</c> as <c>NULL</c>, a flag as <c>1</c> or <c>0</c> and a decimal
    /// without trailing zeros.
    /// </returns>
    public static List<string> Read(IDbConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        using var reader = command.ExecuteReader();
        var rows = new List<string>();
        while (reader.Read())
        {
            var values = new string[reader.FieldCount];
            for (var index = 0; index < values.Length; index++)
                values[index] = reader.IsDBNull(index) ? "NULL" : reader.GetValue(index) switch
                {
                    bool flag => flag ? "1" : "0",
                    // A server's fixed-point column pads its scale, 8.00 for 8, where SQLite has none.
                    decimal number => number.ToString("0.############################", CultureInfo.InvariantCulture),
                    var value => Convert.ToString(value)!,
                };
            rows.Add(string.Join("|", values));
        }

        return rows;
    }

    /// <summary>
    /// Reads one value, as text.
    /// </summary>
    /// <param name="connection">The open connection.</param>
    /// <param name="sql">The query.</param>
    /// <returns>The value, or <c>null</c> for none.</returns>
    public static string? Scalar(IDbConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar() is { } value and not DBNull ? Convert.ToString(value) : null;
    }

    /// <summary>
    /// Runs a coded schema step and checks that it succeeded.
    /// </summary>
    /// <param name="step">The step.</param>
    /// <param name="connection">The open connection.</param>
    public static void Run(Func<object, Tuple<bool, string?>> step, IDbConnection connection)
    {
        var result = step(connection);
        Assert.True(result.Item1, result.Item2);
    }

    #endregion
}
