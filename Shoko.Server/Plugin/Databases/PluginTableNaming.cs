using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace Shoko.Server.Plugin.Databases;

/// <summary>
/// Names the tables of a plugin database kept inside the core's own database,
/// on MySQL or SQL Server: every table and constraint gets a prefix of the
/// plugin and the database, so plugins never meet each other or the core.
/// </summary>
/// <remarks>
/// The prefix is <c>p&lt;8 hex&gt;_&lt;6 hex&gt;_</c>: a hash of the plugin's ID,
/// then a hash of the database's name. The plugin part alone finds every
/// table of a plugin, which is how an uninstalled plugin's data is dropped.
/// Index names are left alone, as both servers scope them to their table.
/// </remarks>
internal sealed class PluginTableNaming
{
    /// <summary>
    /// The name Entity Framework Core gives its migrations history table,
    /// which is prefixed like any other.
    /// </summary>
    public const string HistoryTable = "__EFMigrationsHistory";

    /// <summary>
    /// The longest name MySQL takes for a table or constraint.
    /// </summary>
    public const int MySqlMaxLength = 64;

    /// <summary>
    /// The longest name SQL Server takes for a table or constraint.
    /// </summary>
    public const int SqlServerMaxLength = 128;

    /// <summary>
    /// Creates the naming for a prefix.
    /// </summary>
    /// <param name="prefix">The prefix.</param>
    /// <param name="maxLength">The longest name the server takes.</param>
    public PluginTableNaming(string prefix, int maxLength)
    {
        Prefix = prefix;
        MaxLength = maxLength;
    }

    /// <summary>
    /// The prefix every table and constraint of the database starts with.
    /// </summary>
    public string Prefix { get; }

    /// <summary>
    /// The longest name the server takes.
    /// </summary>
    public int MaxLength { get; }

    /// <summary>
    /// The database's migrations history table.
    /// </summary>
    public string HistoryTableName => Name(HistoryTable);

    #region Prefixes

    /// <summary>
    /// The naming of one plugin database.
    /// </summary>
    /// <param name="pluginID">The plugin's ID.</param>
    /// <param name="databaseName">The database's name within the plugin.</param>
    /// <param name="maxLength">The longest name the server takes.</param>
    /// <returns>The naming.</returns>
    public static PluginTableNaming For(Guid pluginID, string databaseName, int maxLength)
        => new($"{GetPluginPrefix(pluginID)}{Hash(databaseName.ToLowerInvariant(), 6)}_", maxLength);

    /// <summary>
    /// The prefix every table of every database of a plugin starts with.
    /// </summary>
    /// <param name="pluginID">The plugin's ID.</param>
    /// <returns>The prefix, <c>p&lt;8 hex&gt;_</c>.</returns>
    public static string GetPluginPrefix(Guid pluginID)
        => $"p{Hash(pluginID.ToString("D"), 8)}_";

    /// <summary>
    /// A name with the prefix. A name too long for the server keeps its
    /// start and ends in a hash of the whole name instead.
    /// </summary>
    /// <param name="name">The name the plugin gave.</param>
    /// <returns>The name in the core's database.</returns>
    public string Name(string name)
    {
        var prefixed = Prefix + name;
        if (prefixed.Length <= MaxLength)
            return prefixed;

        return $"{Prefix}{name[..(MaxLength - Prefix.Length - 9)]}_{Hash(name, 8)}";
    }

    private static string Hash(string value, int length)
        => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)))[..length];

    #endregion

    #region Migrations

    /// <summary>
    /// Prefixes every table, constraint and sequence the operations name, and
    /// drops what a generated migration would change on the database itself.
    /// </summary>
    /// <param name="operations">The operations of one migration, changed in place.</param>
    /// <exception cref="NotSupportedException">
    /// An operation of a kind the naming does not know, or one on a schema,
    /// which could name a table or schema of the core.
    /// </exception>
    public void Rewrite(IEnumerable<MigrationOperation> operations)
    {
        foreach (var operation in operations)
            Rewrite(operation);
    }

    private void Rewrite(MigrationOperation operation)
    {
        switch (operation)
        {
            case CreateTableOperation create:
                create.Name = Name(create.Name);
                Rewrite(create.Columns);
                if (create.PrimaryKey is not null)
                    Rewrite(create.PrimaryKey);
                Rewrite(create.ForeignKeys);
                Rewrite(create.UniqueConstraints);
                Rewrite(create.CheckConstraints);
                break;
            case AlterTableOperation alter:
                alter.Name = Name(alter.Name);
                alter.OldTable.Name = Name(alter.OldTable.Name);
                break;
            case DropTableOperation drop:
                drop.Name = Name(drop.Name);
                break;
            case RenameTableOperation rename:
                rename.Name = Name(rename.Name);
                if (rename.NewName is not null)
                    rename.NewName = Name(rename.NewName);
                break;
            case AlterColumnOperation alterColumn:
                alterColumn.Table = Name(alterColumn.Table);
                alterColumn.OldColumn.Table = Name(alterColumn.OldColumn.Table);
                break;
            case ColumnOperation column:
                column.Table = Name(column.Table);
                break;
            case DropColumnOperation dropColumn:
                dropColumn.Table = Name(dropColumn.Table);
                break;
            case RenameColumnOperation renameColumn:
                renameColumn.Table = Name(renameColumn.Table);
                break;
            case CreateIndexOperation createIndex:
                createIndex.Table = Name(createIndex.Table);
                break;
            case DropIndexOperation dropIndex:
                if (dropIndex.Table is not null)
                    dropIndex.Table = Name(dropIndex.Table);
                break;
            case RenameIndexOperation renameIndex:
                if (renameIndex.Table is not null)
                    renameIndex.Table = Name(renameIndex.Table);
                break;
            case AddPrimaryKeyOperation addPrimaryKey:
                addPrimaryKey.Table = Name(addPrimaryKey.Table);
                addPrimaryKey.Name = Name(addPrimaryKey.Name);
                break;
            case DropPrimaryKeyOperation dropPrimaryKey:
                dropPrimaryKey.Table = Name(dropPrimaryKey.Table);
                dropPrimaryKey.Name = Name(dropPrimaryKey.Name);
                break;
            case AddForeignKeyOperation addForeignKey:
                addForeignKey.Table = Name(addForeignKey.Table);
                addForeignKey.Name = Name(addForeignKey.Name);
                addForeignKey.PrincipalTable = Name(addForeignKey.PrincipalTable);
                break;
            case DropForeignKeyOperation dropForeignKey:
                dropForeignKey.Table = Name(dropForeignKey.Table);
                dropForeignKey.Name = Name(dropForeignKey.Name);
                break;
            case AddUniqueConstraintOperation addUnique:
                addUnique.Table = Name(addUnique.Table);
                addUnique.Name = Name(addUnique.Name);
                break;
            case DropUniqueConstraintOperation dropUnique:
                dropUnique.Table = Name(dropUnique.Table);
                dropUnique.Name = Name(dropUnique.Name);
                break;
            case AddCheckConstraintOperation addCheck:
                addCheck.Table = Name(addCheck.Table);
                addCheck.Name = Name(addCheck.Name);
                break;
            case DropCheckConstraintOperation dropCheck:
                dropCheck.Table = Name(dropCheck.Table);
                dropCheck.Name = Name(dropCheck.Name);
                break;
            case InsertDataOperation insert:
                insert.Table = Name(insert.Table);
                break;
            case UpdateDataOperation update:
                update.Table = Name(update.Table);
                break;
            case DeleteDataOperation delete:
                delete.Table = Name(delete.Table);
                break;
            case CreateSequenceOperation createSequence:
                createSequence.Name = Name(createSequence.Name);
                break;
            case AlterSequenceOperation alterSequence:
                alterSequence.Name = Name(alterSequence.Name);
                break;
            case RenameSequenceOperation renameSequence:
                renameSequence.Name = Name(renameSequence.Name);
                if (renameSequence.NewName is not null)
                    renameSequence.NewName = Name(renameSequence.NewName);
                break;
            case RestartSequenceOperation restartSequence:
                restartSequence.Name = Name(restartSequence.Name);
                break;
            case DropSequenceOperation dropSequence:
                dropSequence.Name = Name(dropSequence.Name);
                break;
            // The core's database is not the plugin's to change, so the character set and collation
            // a generated migration sets on it are dropped, and nothing is left for it to do.
            case AlterDatabaseOperation alterDatabase:
                Clear(alterDatabase);
                Clear(alterDatabase.OldDatabase);
                break;
            // Raw SQL is the plugin's own.
            case SqlOperation:
                break;
            default:
                throw new NotSupportedException($"The migration operation {operation.GetType().Name} cannot be moved into the core's database, as it may name a table or schema of the core.");
        }
    }

    private static void Clear(DatabaseOperation operation)
    {
        operation.Collation = null;
        foreach (var annotation in operation.GetAnnotations().ToList())
            operation.RemoveAnnotation(annotation.Name);
    }

    #endregion
}
