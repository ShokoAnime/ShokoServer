using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.DependencyInjection;

namespace Shoko.QueueProcessor.Storage.Design;

// Strips HasColumnType() from generated snapshot/designer code so that each provider
// resolves column types from its own native mappings instead of inheriting SQLite
// affinity strings (TEXT, INTEGER) that break SQL Server index creation.
public sealed class PortableAnnotationCodeGenerator(AnnotationCodeGeneratorDependencies dependencies)
    : AnnotationCodeGenerator(dependencies)
{
    public override IReadOnlyList<MethodCallCodeFragment> GenerateFluentApiCalls(
        IProperty property, IDictionary<string, IAnnotation> annotations)
    {
        annotations.Remove("Relational:ColumnType");
        return base.GenerateFluentApiCalls(property, annotations);
    }
}

// Strips explicit column types from operations before code generation so that each provider
// resolves DDL column types from its own native mappings (e.g. uniqueidentifier for Guid on
// SQL Server) rather than the SQLite affinity strings emitted by the design-time context.
// Uses IMigrationsModelDiffer (Relational package) — always loadable without the Design DLL.
internal sealed class PortableMigrationsModelDiffer(IMigrationsModelDiffer inner) : IMigrationsModelDiffer
{
    public bool HasDifferences(IRelationalModel? source, IRelationalModel? target)
        => inner.HasDifferences(source, target);

    public IReadOnlyList<MigrationOperation> GetDifferences(IRelationalModel? source, IRelationalModel? target)
    {
        var operations = inner.GetDifferences(source, target);
        foreach (var op in operations)
        {
            switch (op)
            {
                case CreateTableOperation cto:
                    foreach (var col in cto.Columns)
                        col.ColumnType = null;
                    break;
                case AddColumnOperation aco:
                    aco.ColumnType = null;
                    break;
                case AlterColumnOperation alco:
                    alco.ColumnType = null;
                    alco.OldColumn.ColumnType = null;
                    break;
            }
        }
        return operations;
    }
}

public sealed class QueueDesignTimeServices : IDesignTimeServices
{
    public void ConfigureDesignTimeServices(IServiceCollection services)
    {
        services.AddSingleton<IAnnotationCodeGenerator, PortableAnnotationCodeGenerator>();

        var innerDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IMigrationsModelDiffer));
        if (innerDescriptor == null) return;
        services.Remove(innerDescriptor);
        services.Add(new ServiceDescriptor(
            typeof(IMigrationsModelDiffer),
            sp => new PortableMigrationsModelDiffer(
                innerDescriptor.ImplementationType != null
                    ? (IMigrationsModelDiffer)ActivatorUtilities.CreateInstance(sp, innerDescriptor.ImplementationType)
                    : (IMigrationsModelDiffer)innerDescriptor.ImplementationFactory!(sp)),
            ServiceLifetime.Scoped));
    }
}
