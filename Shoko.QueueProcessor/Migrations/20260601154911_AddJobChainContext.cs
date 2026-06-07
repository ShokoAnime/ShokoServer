using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace Shoko.QueueProcessor.Migrations
{
    /// <inheritdoc />
    public partial class AddJobChainContext : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ChainId",
                table: "Jobs",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsChainFinally",
                table: "Jobs",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "ParentJobId",
                table: "Jobs",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "JobChains",
                columns: table => new
                {
                    ChainId = table.Column<Guid>(nullable: false),
                    Status = table.Column<int>(nullable: false),
                    DataJson = table.Column<string>(nullable: true),
                    ResultsJson = table.Column<string>(nullable: true),
                    OutcomesJson = table.Column<string>(nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(nullable: false)
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
