using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NAVYCommerce.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerAddressRegionCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CityCode",
                table: "customer_addresses",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "DistrictCode",
                table: "customer_addresses",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ProvinceCode",
                table: "customer_addresses",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "VillageCode",
                table: "customer_addresses",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CityCode",
                table: "customer_addresses");

            migrationBuilder.DropColumn(
                name: "DistrictCode",
                table: "customer_addresses");

            migrationBuilder.DropColumn(
                name: "ProvinceCode",
                table: "customer_addresses");

            migrationBuilder.DropColumn(
                name: "VillageCode",
                table: "customer_addresses");
        }
    }
}
