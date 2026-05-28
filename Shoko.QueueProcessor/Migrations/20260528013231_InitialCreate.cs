using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shoko.QueueProcessor.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Jobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    JobType = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    JobKey = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    JobDataJson = table.Column<string>(type: "TEXT", maxLength: 4096, nullable: true),
                    Priority = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    QueuedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    ScheduledAt = table.Column<long>(type: "INTEGER", nullable: true),
                    RetryCount = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0)
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
