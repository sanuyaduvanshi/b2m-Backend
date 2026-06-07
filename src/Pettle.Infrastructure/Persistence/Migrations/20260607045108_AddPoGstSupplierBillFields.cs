using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pettle.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPoGstSupplierBillFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AccountLedger",
                schema: "pettle",
                table: "PurchaseOrders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AdditionalCharges",
                schema: "pettle",
                table: "PurchaseOrders",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountAmount",
                schema: "pettle",
                table: "PurchaseOrders",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateOnly>(
                name: "DueDate",
                schema: "pettle",
                table: "PurchaseOrders",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ExportSez",
                schema: "pettle",
                table: "PurchaseOrders",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "FlatDiscountAmount",
                schema: "pettle",
                table: "PurchaseOrders",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "FlatDiscountPercent",
                schema: "pettle",
                table: "PurchaseOrders",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "GrossAmount",
                schema: "pettle",
                table: "PurchaseOrders",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "MaterialInwardNo",
                schema: "pettle",
                table: "PurchaseOrders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentTerm",
                schema: "pettle",
                table: "PurchaseOrders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReferenceBillNumber",
                schema: "pettle",
                table: "PurchaseOrders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ReverseCharge",
                schema: "pettle",
                table: "PurchaseOrders",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "RoundOff",
                schema: "pettle",
                table: "PurchaseOrders",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ShippingDate",
                schema: "pettle",
                table: "PurchaseOrders",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TaxType",
                schema: "pettle",
                table: "PurchaseOrders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TaxableAmount",
                schema: "pettle",
                table: "PurchaseOrders",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "FreeQuantity",
                schema: "pettle",
                table: "PurchaseOrderLines",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "ItemCode",
                schema: "pettle",
                table: "PurchaseOrderLines",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LandingCost",
                schema: "pettle",
                table: "PurchaseOrderLines",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "Mrp",
                schema: "pettle",
                table: "PurchaseOrderLines",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PurDisc1Percent",
                schema: "pettle",
                table: "PurchaseOrderLines",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PurDisc2Percent",
                schema: "pettle",
                table: "PurchaseOrderLines",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SellingPrice",
                schema: "pettle",
                table: "PurchaseOrderLines",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TaxAmount",
                schema: "pettle",
                table: "PurchaseOrderLines",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TaxableAmount",
                schema: "pettle",
                table: "PurchaseOrderLines",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "Unit",
                schema: "pettle",
                table: "PurchaseOrderLines",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccountLedger",
                schema: "pettle",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "AdditionalCharges",
                schema: "pettle",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "DiscountAmount",
                schema: "pettle",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "DueDate",
                schema: "pettle",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "ExportSez",
                schema: "pettle",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "FlatDiscountAmount",
                schema: "pettle",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "FlatDiscountPercent",
                schema: "pettle",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "GrossAmount",
                schema: "pettle",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "MaterialInwardNo",
                schema: "pettle",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "PaymentTerm",
                schema: "pettle",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "ReferenceBillNumber",
                schema: "pettle",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "ReverseCharge",
                schema: "pettle",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "RoundOff",
                schema: "pettle",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "ShippingDate",
                schema: "pettle",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "TaxType",
                schema: "pettle",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "TaxableAmount",
                schema: "pettle",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "FreeQuantity",
                schema: "pettle",
                table: "PurchaseOrderLines");

            migrationBuilder.DropColumn(
                name: "ItemCode",
                schema: "pettle",
                table: "PurchaseOrderLines");

            migrationBuilder.DropColumn(
                name: "LandingCost",
                schema: "pettle",
                table: "PurchaseOrderLines");

            migrationBuilder.DropColumn(
                name: "Mrp",
                schema: "pettle",
                table: "PurchaseOrderLines");

            migrationBuilder.DropColumn(
                name: "PurDisc1Percent",
                schema: "pettle",
                table: "PurchaseOrderLines");

            migrationBuilder.DropColumn(
                name: "PurDisc2Percent",
                schema: "pettle",
                table: "PurchaseOrderLines");

            migrationBuilder.DropColumn(
                name: "SellingPrice",
                schema: "pettle",
                table: "PurchaseOrderLines");

            migrationBuilder.DropColumn(
                name: "TaxAmount",
                schema: "pettle",
                table: "PurchaseOrderLines");

            migrationBuilder.DropColumn(
                name: "TaxableAmount",
                schema: "pettle",
                table: "PurchaseOrderLines");

            migrationBuilder.DropColumn(
                name: "Unit",
                schema: "pettle",
                table: "PurchaseOrderLines");
        }
    }
}
