using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NAVYCommerce.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProductGalleryAndOptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_product_variants_ProductId",
                table: "product_variants");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_product_variants_ProductId_Id",
                table: "product_variants",
                columns: new[] { "ProductId", "Id" });

            migrationBuilder.CreateTable(
                name: "product_images",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    AltText = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_images", x => x.Id);
                    table.UniqueConstraint("AK_product_images_ProductId_Id", x => new { x.ProductId, x.Id });
                    table.ForeignKey(
                        name: "FK_product_images_products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "product_options",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_options", x => x.Id);
                    table.UniqueConstraint("AK_product_options_ProductId_Id", x => new { x.ProductId, x.Id });
                    table.ForeignKey(
                        name: "FK_product_options_products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "product_option_values",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    OptionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Value = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    ImageId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_option_values", x => x.Id);
                    table.UniqueConstraint("AK_product_option_values_ProductId_OptionId_Id", x => new { x.ProductId, x.OptionId, x.Id });
                    table.ForeignKey(
                        name: "FK_product_option_values_product_images_ProductId_ImageId",
                        columns: x => new { x.ProductId, x.ImageId },
                        principalTable: "product_images",
                        principalColumns: new[] { "ProductId", "Id" });
                    table.ForeignKey(
                        name: "FK_product_option_values_product_options_ProductId_OptionId",
                        columns: x => new { x.ProductId, x.OptionId },
                        principalTable: "product_options",
                        principalColumns: new[] { "ProductId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "product_variant_option_values",
                columns: table => new
                {
                    VariantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OptionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    OptionValueId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_variant_option_values", x => new { x.VariantId, x.OptionId });
                    table.ForeignKey(
                        name: "FK_product_variant_option_values_product_option_values_Product~",
                        columns: x => new { x.ProductId, x.OptionId, x.OptionValueId },
                        principalTable: "product_option_values",
                        principalColumns: new[] { "ProductId", "OptionId", "Id" });
                    table.ForeignKey(
                        name: "FK_product_variant_option_values_product_variants_ProductId_Va~",
                        columns: x => new { x.ProductId, x.VariantId },
                        principalTable: "product_variants",
                        principalColumns: new[] { "ProductId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_product_images_ProductId_SortOrder",
                table: "product_images",
                columns: new[] { "ProductId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_product_option_values_OptionId_Value",
                table: "product_option_values",
                columns: new[] { "OptionId", "Value" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_product_option_values_ProductId_ImageId",
                table: "product_option_values",
                columns: new[] { "ProductId", "ImageId" });

            migrationBuilder.CreateIndex(
                name: "IX_product_options_ProductId_Name",
                table: "product_options",
                columns: new[] { "ProductId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_product_variant_option_values_ProductId_OptionId_OptionValu~",
                table: "product_variant_option_values",
                columns: new[] { "ProductId", "OptionId", "OptionValueId" });

            migrationBuilder.CreateIndex(
                name: "IX_product_variant_option_values_ProductId_VariantId",
                table: "product_variant_option_values",
                columns: new[] { "ProductId", "VariantId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "product_variant_option_values");

            migrationBuilder.DropTable(
                name: "product_option_values");

            migrationBuilder.DropTable(
                name: "product_images");

            migrationBuilder.DropTable(
                name: "product_options");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_product_variants_ProductId_Id",
                table: "product_variants");

            migrationBuilder.CreateIndex(
                name: "IX_product_variants_ProductId",
                table: "product_variants",
                column: "ProductId");
        }
    }
}
