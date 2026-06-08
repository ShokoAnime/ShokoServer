using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace Shoko.QueueProcessor.Migrations
{
    /// <inheritdoc />
    public partial class FixQueueTimestampColumns : Migration
    {
        // The InitialCreate migration originally used bigint for QueuedAt and ScheduledAt
        // on all providers. SQLite is correct (DateTimeOffset stored as unix milliseconds
        // via a ValueConverter), but SQL Server and MySQL need their native timestamp types
        // because those contexts have no ValueConverter.
        //
        // Each provider block is conditional: it checks whether the column is still bigint
        // before altering, making this a no-op on fresh installs that ran the corrected
        // InitialCreate migration (which already creates datetimeoffset / datetime(6) columns).

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (ActiveProvider == "Microsoft.EntityFrameworkCore.SqlServer")
            {
                // Idempotent: only alters if QueuedAt is still bigint.
                // Named DEFAULT constraint avoids auto-generated name ambiguity.
                migrationBuilder.Sql(@"
IF OBJECT_ID('Jobs', 'U') IS NOT NULL
AND EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('Jobs')
      AND name = 'QueuedAt'
      AND user_type_id = TYPE_ID('bigint')
)
BEGIN
    DROP INDEX IX_QueuedJobs_Startup ON Jobs;

    ALTER TABLE Jobs DROP COLUMN QueuedAt;
    ALTER TABLE Jobs ADD QueuedAt datetimeoffset(7) NOT NULL
        CONSTRAINT DF_Jobs_QueuedAt DEFAULT SYSDATETIMEOFFSET();

    ALTER TABLE Jobs DROP COLUMN ScheduledAt;
    ALTER TABLE Jobs ADD ScheduledAt datetimeoffset(7) NULL;

    CREATE INDEX IX_QueuedJobs_Startup ON Jobs (JobType, ScheduledAt, Priority, QueuedAt);
END
");
            }
            else if (ActiveProvider == "Pomelo.EntityFrameworkCore.MySql")
            {
                // MySQL stored procedures can use IF, but plain SQL batches cannot.
                // Use a temporary procedure to emulate the conditional check.
                migrationBuilder.Sql(@"
DROP PROCEDURE IF EXISTS _shoko_fix_queue_timestamps;
CREATE PROCEDURE _shoko_fix_queue_timestamps()
BEGIN
    DECLARE col_type VARCHAR(64);
    SELECT DATA_TYPE INTO col_type
    FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'Jobs'
      AND COLUMN_NAME = 'QueuedAt';

    IF col_type = 'bigint' THEN
        -- Clear existing rows before column type change: MySQL strict mode rejects
        -- MODIFY COLUMN when rows contain bigint (Unix-ms) values that cannot be
        -- parsed as datetime. The Jobs table is an ephemeral crash-recovery store;
        -- any surviving rows will be re-queued by Shoko on next startup.
        DELETE FROM Jobs;
        ALTER TABLE Jobs DROP INDEX IX_QueuedJobs_Startup;
        ALTER TABLE Jobs MODIFY COLUMN QueuedAt datetime(6) NOT NULL;
        ALTER TABLE Jobs MODIFY COLUMN ScheduledAt datetime(6) NULL;
        CREATE INDEX IX_QueuedJobs_Startup ON Jobs (JobType, ScheduledAt, Priority, QueuedAt);
    END IF;
END;
CALL _shoko_fix_queue_timestamps();
DROP PROCEDURE _shoko_fix_queue_timestamps;
");
            }
            // SQLite: no change needed — bigint columns via ValueConverter are correct.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // SQL Server: drop the named DEFAULT constraint created in Up() before dropping
            // the column — SQL Server rejects DROP COLUMN when a constraint is still attached.
            if (ActiveProvider == "Microsoft.EntityFrameworkCore.SqlServer")
                migrationBuilder.Sql("IF OBJECT_ID('DF_Jobs_QueuedAt', 'D') IS NOT NULL ALTER TABLE Jobs DROP CONSTRAINT DF_Jobs_QueuedAt;");
            else if (ActiveProvider != "Pomelo.EntityFrameworkCore.MySql")
                return; // SQLite: no rollback needed

            migrationBuilder.DropIndex("IX_QueuedJobs_Startup", "Jobs");

            migrationBuilder.DropColumn("QueuedAt", "Jobs");
            migrationBuilder.AddColumn<long>(
                name: "QueuedAt",
                table: "Jobs",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.DropColumn("ScheduledAt", "Jobs");
            migrationBuilder.AddColumn<long>(
                name: "ScheduledAt",
                table: "Jobs",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_QueuedJobs_Startup",
                table: "Jobs",
                columns: new[] { "JobType", "ScheduledAt", "Priority", "QueuedAt" });
        }
    }
}
