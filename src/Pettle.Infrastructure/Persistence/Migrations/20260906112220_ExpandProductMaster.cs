using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pettle.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExpandProductMaster : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AdditionalInfo",
                schema: "pettle",
                table: "Products",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CessPercent",
                schema: "pettle",
                table: "Products",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                schema: "pettle",
                table: "Products",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Ingredients",
                schema: "pettle",
                table: "Products",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPurchaseTaxInclusive",
                schema: "pettle",
                table: "Products",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsSalesTaxInclusive",
                schema: "pettle",
                table: "Products",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "LandingCost",
                schema: "pettle",
                table: "Products",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "ManageMultipleBatch",
                schema: "pettle",
                table: "Products",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "NetWeightUnit",
                schema: "pettle",
                table: "Products",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Nutrition",
                schema: "pettle",
                table: "Products",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PrintName",
                schema: "pettle",
                table: "Products",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProductType",
                schema: "pettle",
                table: "Products",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PurchasePrice",
                schema: "pettle",
                table: "Products",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PurchaseTaxPercent",
                schema: "pettle",
                table: "Products",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SalesTaxPercent",
                schema: "pettle",
                table: "Products",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SellingDiscountPercent",
                schema: "pettle",
                table: "Products",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "ShortDescription",
                schema: "pettle",
                table: "Products",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubBrand",
                schema: "pettle",
                table: "Products",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubCategory",
                schema: "pettle",
                table: "Products",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Unit",
                schema: "pettle",
                table: "Products",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AdditionalInfo",
                schema: "pettle",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "CessPercent",
                schema: "pettle",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Description",
                schema: "pettle",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Ingredients",
                schema: "pettle",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "IsPurchaseTaxInclusive",
                schema: "pettle",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "IsSalesTaxInclusive",
                schema: "pettle",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "LandingCost",
                schema: "pettle",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "ManageMultipleBatch",
                schema: "pettle",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "NetWeightUnit",
                schema: "pettle",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Nutrition",
                schema: "pettle",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "PrintName",
                schema: "pettle",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "ProductType",
                schema: "pettle",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "PurchasePrice",
                schema: "pettle",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "PurchaseTaxPercent",
                schema: "pettle",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "SalesTaxPercent",
                schema: "pettle",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "SellingDiscountPercent",
                schema: "pettle",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "ShortDescription",
                schema: "pettle",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "SubBrand",
                schema: "pettle",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "SubCategory",
                schema: "pettle",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Unit",
                schema: "pettle",
                table: "Products");
        }
    }
}
