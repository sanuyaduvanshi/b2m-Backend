using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pettle.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPurchaseDebitNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DocType",
                schema: "pettle",
                table: "PurchaseOrders",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "ReturnAgainstPurchaseOrderId",
                schema: "pettle",
                table: "PurchaseOrders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReturnReason",
                schema: "pettle",
                table: "PurchaseOrders",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_ReturnAgainstPurchaseOrderId",
                schema: "pettle",
                table: "PurchaseOrders",
                column: "ReturnAgainstPurchaseOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_TenantId_DocType_PurchaseDate",
                schema: "pettle",
                table: "PurchaseOrders",
                columns: new[] { "TenantId", "DocType", "PurchaseDate" });

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseOrders_PurchaseOrders_ReturnAgainstPurchaseOrderId",
                schema: "pettle",
                table: "PurchaseOrders",
                column: "ReturnAgainstPurchaseOrderId",
                principalSchema: "pettle",
                principalTable: "PurchaseOrders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseOrders_PurchaseOrders_ReturnAgainstPurchaseOrderId",
                schema: "pettle",
                table: "PurchaseOrders");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrders_ReturnAgainstPurchaseOrderId",
                schema: "pettle",
                table: "PurchaseOrders");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrders_TenantId_DocType_PurchaseDate",
                schema: "pettle",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "DocType",
                schema: "pettle",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "ReturnAgainstPurchaseOrderId",
                schema: "pettle",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "ReturnReason",
                schema: "pettle",
                table: "PurchaseOrders");
        }
    }
}
