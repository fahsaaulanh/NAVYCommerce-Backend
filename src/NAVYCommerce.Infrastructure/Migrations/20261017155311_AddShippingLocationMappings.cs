using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NAVYCommerce.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddShippingLocationMappings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "shipping_location_mappings",
                columns: table => new
                {
                    Provider = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    RegionFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DestinationId = table.Column<int>(type: "integer", nullable: false),
                    DestinationLabel = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ConfirmedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shipping_location_mappings", x => new { x.Provider, x.RegionFingerprint });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "shipping_location_mappings");
        }
    }
}
