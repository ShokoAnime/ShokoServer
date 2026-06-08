using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace Shoko.QueueProcessor.Migrations
{
    /// <inheritdoc />
    public partial class AddJobChainContext : QueueMigrationBase
    {
        private string BoolType() => ActiveProvider switch
        {
            "Microsoft.EntityFrameworkCore.SqlServer" => "bit",
            "Pomelo.EntityFrameworkCore.MySql" => "tinyint(1)",
            _ => "INTEGER"
        };

        private string StringMaxType() => ActiveProvider switch
        {
            "Microsoft.EntityFrameworkCore.SqlServer" => "nvarchar(max)",
            "Pomelo.EntityFrameworkCore.MySql" => "longtext",
            _ => "TEXT"
        };

        private string DateTimeOffsetType() => ActiveProvider switch
        {
            "Microsoft.EntityFrameworkCore.SqlServer" => "datetimeoffset(7)",
            "Pomelo.EntityFrameworkCore.MySql" => "datetime(6)",
            _ => "TEXT"
        };

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ChainId",
                table: "Jobs",
                type: GuidType(),
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsChainFinally",
                table: "Jobs",
                type: BoolType(),
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "ParentJobId",
                table: "Jobs",
                type: GuidType(),
                nullable: true);

            migrationBuilder.CreateTable(
                name: "JobChains",
                columns: table => new
                {
                    ChainId = table.Column<Guid>(type: GuidType(), nullable: false),
                    Status = table.Column<int>(type: IntType(), nullable: false),
                    DataJson = table.Column<string>(type: StringMaxType(), nullable: true),
                    ResultsJson = table.Column<string>(type: StringMaxType(), nullable: true),
                    OutcomesJson = table.Column<string>(type: StringMaxType(), nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: DateTimeOffsetType(), nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: DateTimeOffsetType(), nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobChains", x => x.ChainId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_QueuedJobs_ChainId",
                table: "Jobs",
                column: "ChainId");

            migrationBuilder.CreateIndex(
                name: "IX_QueuedJobs_ParentJobId",
                table: "Jobs",
                column: "ParentJobId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JobChains");

            migrationBuilder.DropIndex(
                name: "IX_QueuedJobs_ChainId",
                table: "Jobs");

            migrationBuilder.DropIndex(
                name: "IX_QueuedJobs_ParentJobId",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "ChainId",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "IsChainFinally",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "ParentJobId",
                table: "Jobs");
        }
    }
}
