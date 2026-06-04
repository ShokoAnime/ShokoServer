using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Shoko.QueueProcessor.Migrations
{
    /// <inheritdoc />
    public partial class DropQuartzTables : Migration
    {
        // Drop order respects FK dependencies: child trigger tables before QRTZ_TRIGGERS, QRTZ_TRIGGERS before QRTZ_JOB_DETAILS.
        private static readonly string[] _quartzTables =
        [
            "QRTZ_SIMPROP_TRIGGERS",
            "QRTZ_BLOB_TRIGGERS",
            "QRTZ_CRON_TRIGGERS",
            "QRTZ_SIMPLE_TRIGGERS",
            "QRTZ_FIRED_TRIGGERS",
            "QRTZ_TRIGGERS",
            "QRTZ_JOB_DETAILS",
            "QRTZ_CALENDARS",
            "QRTZ_PAUSED_TRIGGER_GRPS",
            "QRTZ_SCHEDULER_STATE",
            "QRTZ_LOCKS",
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var table in _quartzTables)
                migrationBuilder.Sql($"DROP TABLE IF EXISTS {table};");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
