using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace Shoko.QueueProcessor.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : QueueMigrationBase
    {
        private string StringType(int len) => ActiveProvider switch
        {
            "Microsoft.EntityFrameworkCore.SqlServer" => len <= 4000 ? $"nvarchar({len})" : "nvarchar(max)",
            "Pomelo.EntityFrameworkCore.MySql" => $"varchar({len})",
            _ => "TEXT"
        };

        // SQLite has no native DateTimeOffset; the SqliteQueueDbContext converter stores
        // DateTimeOffset as unix milliseconds (long) in an INTEGER column. SQL Server and
        // MySQL map DateTimeOffset to their native timestamp types without a converter.
        private string DateTimeOffsetType() => ActiveProvider switch
        {
            "Microsoft.EntityFrameworkCore.SqlServer" => "datetimeoffset(7)",
            "Pomelo.EntityFrameworkCore.MySql" => "datetime(6)",
            _ => "INTEGER"
        };

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Jobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: GuidType(), nullable: false),
                    JobType = table.Column<string>(type: StringType(256), maxLength: 256, nullable: false),
                    JobKey = table.Column<string>(type: StringType(512), maxLength: 512, nullable: false),
                    JobDataJson = table.Column<string>(type: StringType(4096), maxLength: 4096, nullable: true),
                    Priority = table.Column<int>(type: IntType(), nullable: false, defaultValue: 0),
                    QueuedAt = table.Column<DateTimeOffset>(type: DateTimeOffsetType(), nullable: false),
                    ScheduledAt = table.Column<DateTimeOffset>(type: DateTimeOffsetType(), nullable: true),
                    RetryCount = table.Column<int>(type: IntType(), nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Jobs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_QueuedJobs_JobKey",
                table: "Jobs",
                column: "JobKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QueuedJobs_Startup",
                table: "Jobs",
                columns: new[] { "JobType", "ScheduledAt", "Priority", "QueuedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Jobs");
        }
    }
}
