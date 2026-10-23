using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Server.Databases;

public partial class DatabaseFixes
{
    #region Filter Parameters | Tables

    /// <summary>
    /// The kinds of value a stored filter parameter is turned into.
    /// </summary>
    private enum StoredFilterValueKind
    {
        Number,
        Text,
        YearlySeason,
        CastRole,
        CrewRole,
    }

    /// <summary>
    /// The expressions that stored their values under names of their own: for
    /// each, the old key, the key it moves to and what its value becomes.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, (string From, string To, StoredFilterValueKind Kind)[]> _storedFilterParameters =
        new Dictionary<string, (string From, string To, StoredFilterValueKind Kind)[]>(StringComparer.Ordinal)
        {
            ["InSeasonExpression"] = [("Year", "Parameter", StoredFilterValueKind.Number), ("Season", "SecondParameter", StoredFilterValueKind.YearlySeason)],
            ["InYearExpression"] = [("Parameter", "Parameter", StoredFilterValueKind.Number)],
            ["HasCharacterExpression"] = [("CharacterID", "Parameter", StoredFilterValueKind.Text)],
            ["HasCharacterWithAppearanceExpression"] = [("CharacterID", "Parameter", StoredFilterValueKind.Text), ("Appearance", "SecondParameter", StoredFilterValueKind.CastRole)],
            ["HasCreatorExpression"] = [("CreatorID", "Parameter", StoredFilterValueKind.Text)],
            ["HasCreatorWithRoleExpression"] = [("CreatorID", "Parameter", StoredFilterValueKind.Text), ("Role", "SecondParameter", StoredFilterValueKind.CrewRole)],
        };

    #endregion

    #region Filter Parameters | Steps

    /// <summary>
    /// Moves the values of the stored filter presets that kept them under
    /// names of their own to <c>Parameter</c> and <c>SecondParameter</c>, the
    /// names every expression is stored under now.
    /// </summary>
    /// <remarks>
    /// It runs among the schema steps, before the presets are loaded, so the
    /// filters never load without their values. A season, appearance or role
    /// becomes its name; one that no longer exists becomes unset.
    /// </remarks>
    /// <param name="connection">The open connection to the database.</param>
    /// <returns>Whether it ran, and the error when it did not.</returns>
    public static Tuple<bool, string?> MigrateStoredFilterParameters(object connection)
    {
        try
        {
            var dbConnection = (DbConnection)connection;
            if (dbConnection.State is not ConnectionState.Open)
                dbConnection.Open();

            using var transaction = dbConnection.BeginTransaction();
            var changed = 0;
            foreach (var row in Read(transaction, "SELECT FilterPresetID, Expression, SortingExpression FROM FilterPreset"))
            {
                var expression = row[1] as string;
                var sorting = row[2] as string;
                var newExpression = expression is null ? null : RewriteStoredFilterParameters(expression);
                var newSorting = sorting is null ? null : RewriteStoredFilterParameters(sorting);
                if (newExpression is null && newSorting is null)
                    continue;

                using var command = dbConnection.CreateCommand();
                command.Transaction = transaction;
                var id = Convert.ToInt32(row[0], CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
                command.CommandText = $"UPDATE FilterPreset SET Expression = @Expression, SortingExpression = @SortingExpression WHERE FilterPresetID = {id}";
                AddParameter(command, "@Expression", newExpression ?? expression);
                AddParameter(command, "@SortingExpression", newSorting ?? sorting);
                command.ExecuteNonQuery();
                changed++;
            }

            transaction.Commit();
            _logger.Info("Moved the values of {Count} stored filter presets to Parameter and SecondParameter.", changed);
            return new(true, null);
        }
        catch (Exception ex)
        {
            return new(false, ex.ToString());
        }
    }

    #endregion

    #region Filter Parameters | Helpers

    /// <summary>
    /// Rewrites a stored filter's JSON so every expression in it keeps its
    /// values under <c>Parameter</c> and <c>SecondParameter</c>.
    /// </summary>
    /// <param name="json">The stored JSON of an expression or a sorting chain.</param>
    /// <returns>
    /// The rewritten JSON, or <c>null</c> when nothing in it needed to change
    /// or it is not JSON.
    /// </returns>
    internal static string? RewriteStoredFilterParameters(string json)
    {
        JToken root;
        try
        {
            root = JToken.Parse(json);
        }
        catch (JsonException)
        {
            return null;
        }

        var changed = false;
        var nodes = root is JContainer container ? container.DescendantsAndSelf().OfType<JObject>().ToList() : [];
        foreach (var node in nodes)
        {
            if (node["$type"] is not JValue { Type: JTokenType.String } typeValue ||
                !_storedFilterParameters.TryGetValue(SimpleTypeName((string)typeValue!), out var moves))
                continue;

            foreach (var (from, to, kind) in moves)
            {
                if (node.Property(from) is not { } property)
                    continue;

                var value = StoredFilterValue(property.Value, kind);
                if (from == to)
                {
                    if (JToken.DeepEquals(property.Value, value))
                        continue;

                    property.Value = value;
                }
                else
                {
                    property.Remove();
                    if (node.Property(to) is null)
                        node[to] = value;
                }

                changed = true;
            }
        }

        return changed ? root.ToString(Formatting.None) : null;
    }

    /// <summary>
    /// The class name a stored <c>$type</c> names, without its namespace or
    /// assembly.
    /// </summary>
    /// <param name="type">The stored type name.</param>
    /// <returns>The simple class name.</returns>
    private static string SimpleTypeName(string type)
        => type.Split(',')[0].Trim().Split('.')[^1];

    /// <summary>
    /// A stored value as the container interface takes it.
    /// </summary>
    /// <param name="value">The stored value.</param>
    /// <param name="kind">What it becomes.</param>
    /// <returns>The value to store, a JSON null when it is unset or can no longer be read.</returns>
    private static JToken StoredFilterValue(JToken value, StoredFilterValueKind kind)
    {
        if (value.Type is JTokenType.Null or JTokenType.Undefined)
            return JValue.CreateNull();

        return kind switch
        {
            StoredFilterValueKind.Number => value.Type is JTokenType.Integer or JTokenType.Float
                ? new JValue(value.Value<double>())
                : double.TryParse(value.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? new JValue(number) : JValue.CreateNull(),
            StoredFilterValueKind.Text => value.Type is JTokenType.String ? value : new JValue(value.ToString()),
            StoredFilterValueKind.YearlySeason => EnumName<YearlySeason>(value),
            StoredFilterValueKind.CastRole => EnumName<CastRoleType>(value),
            StoredFilterValueKind.CrewRole => EnumName<CrewRoleType>(value),
            _ => value,
        };
    }

    /// <summary>
    /// The name of a stored enum value, read from its number or from its
    /// name, ignoring case and underscores.
    /// </summary>
    /// <typeparam name="TEnum">The enum.</typeparam>
    /// <param name="value">The stored value.</param>
    /// <returns>The value's name, or a JSON null for one the enum no longer has.</returns>
    private static JToken EnumName<TEnum>(JToken value) where TEnum : struct, Enum
    {
        TEnum parsed;
        if (value.Type is JTokenType.Integer)
            parsed = (TEnum)Enum.ToObject(typeof(TEnum), value.Value<long>());
        else if (!Enum.TryParse(value.ToString().Replace("_", string.Empty), ignoreCase: true, out parsed))
            return JValue.CreateNull();

        return Enum.IsDefined(parsed) ? new JValue(parsed.ToString()) : JValue.CreateNull();
    }

    #endregion
}
