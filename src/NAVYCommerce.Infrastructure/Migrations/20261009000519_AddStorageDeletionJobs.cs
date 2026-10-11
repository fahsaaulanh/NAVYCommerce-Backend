using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NAVYCommerce.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStorageDeletionJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "storage_deletion_jobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    NextAttemptAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_storage_deletion_jobs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_storage_deletion_jobs_NextAttemptAt_CreatedAt",
                table: "storage_deletion_jobs",
                columns: new[] { "NextAttemptAt", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_storage_deletion_jobs_StorageKey",
                table: "storage_deletion_jobs",
                column: "StorageKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "storage_deletion_jobs");
        }
    }
}
