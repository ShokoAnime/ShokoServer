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
    private HashSet<(string Table, string Column)> MappedColumns<TUserType>()
    {
        var factory = fixture.Services.GetRequiredService<DatabaseFactory>().SessionFactory;
        var columns = new HashSet<(string Table, string Column)>();
        foreach (var metadata in factory.GetAllClassMetadata().Values)
        {
            var persister = (AbstractEntityPersister)metadata;
            foreach (var property in metadata.PropertyNames)
            {
                if (metadata.GetPropertyType(property) is not CustomType { UserType: TUserType })
                    continue;

                foreach (var column in persister.GetPropertyColumnNames(property))
                    columns.Add((persister.TableName, column));
            }
        }

        return columns;
    }

    #endregion

    #region Coverage

    [Fact]
    public void EverySourceColumnIsChecked()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);

        var mapped = MappedColumns<MetadataSourceType>();

        Assert.NotEmpty(mapped);
        Assert.Equal(mapped.OrderBy(c => c).ToList(), DatabaseFixes.MetadataSourceColumns.Distinct().OrderBy(c => c).ToList());
    }

    [Fact]
    public void EveryEntityTypeColumnIsChecked()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);

        var mapped = MappedColumns<MetadataEntityTypeType>();

        Assert.NotEmpty(mapped);
        Assert.Equal(mapped.OrderBy(c => c).ToList(), DatabaseFixes.MetadataEntityTypeColumns.Distinct().OrderBy(c => c).ToList());
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
