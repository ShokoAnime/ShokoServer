using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using NHibernate.Persister.Entity;
using NHibernate.Type;
using Shoko.Server.Databases;
using Shoko.Server.Databases.NHibernate;
using Xunit;

namespace Shoko.IntegrationTests;

/// <summary>
/// Checks that the startup check of stored source and entity type numbers
/// reads every column the mappings store them in, and that each of those
/// columns exists in the migrated database.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class MetadataNumberColumnTests(DatabaseMigrationFixture fixture)
{
    #region Helpers

    /// <summary>
    /// Every mapped column whose property is stored through the given user type.
    /// </summary>
    private HashSet<(string Table, string Column)> MappedColumns(Type userType)
    {
        var factory = fixture.Services.GetRequiredService<DatabaseFactory>().SessionFactory;
        var columns = new HashSet<(string Table, string Column)>();
        foreach (var metadata in factory.GetAllClassMetadata().Values)
        {
            var persister = (AbstractEntityPersister)metadata;
            foreach (var property in metadata.PropertyNames)
            {
                if (metadata.GetPropertyType(property) is not CustomType { UserType: { } mappedType } || !userType.IsInstanceOfType(mappedType))
                    continue;

                foreach (var column in persister.GetPropertyColumnNames(property))
                    columns.Add((persister.TableName, column));
            }
        }

        return columns;
    }

    #endregion

    #region Coverage

    /// <summary>Each user type with the columns the startup check reads for it.</summary>
    public static TheoryData<Type, IReadOnlyList<(string Table, string Column)>> CheckedColumns => new()
    {
        { typeof(MetadataSourceType), DatabaseFixes.MetadataSourceColumns },
        { typeof(MetadataEntityTypeType), DatabaseFixes.MetadataEntityTypeColumns },
    };

    [Theory]
    [MemberData(nameof(CheckedColumns))]
    public void EveryMappedColumnIsChecked(Type userType, IReadOnlyList<(string Table, string Column)> checkedColumns)
    {
        Assert.True(fixture.Success, fixture.FailureMessage);

        var mapped = MappedColumns(userType);

        Assert.NotEmpty(mapped);
        Assert.Equal(mapped.OrderBy(c => c).ToList(), checkedColumns.Distinct().OrderBy(c => c).ToList());
    }

    [Fact]
    public void EveryCheckedColumnCanBeRead()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);

        using var connection = fixture.OpenConnection();
        foreach (var (table, column) in DatabaseFixes.MetadataSourceColumns.Concat(DatabaseFixes.MetadataEntityTypeColumns))
        {
            using var command = connection.CreateCommand();
            command.CommandText = $"SELECT DISTINCT {column} FROM {table}";
            using var reader = command.ExecuteReader();
            while (reader.Read())
                Assert.InRange(Convert.ToInt64(reader.GetValue(0)), 0, 255);
        }
    }

    #endregion
}
