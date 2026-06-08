using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace Shoko.QueueProcessor.Migrations
{
    /// <summary>
    /// Base class for queue migrations that provides provider-appropriate column type strings.
    /// Centralises the ActiveProvider switch expressions so each migration class does not need
    /// to re-declare them.
    /// </summary>
    public abstract class QueueMigrationBase : Migration
    {
        protected string GuidType() => ActiveProvider switch
        {
            "Microsoft.EntityFrameworkCore.SqlServer" => "uniqueidentifier",
            "Pomelo.EntityFrameworkCore.MySql" => "char(36)",
            _ => "TEXT"
        };

        protected string IntType() => ActiveProvider switch
        {
            "Microsoft.EntityFrameworkCore.SqlServer" or "Pomelo.EntityFrameworkCore.MySql" => "int",
            _ => "INTEGER"
        };
    }
}
