using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pettle.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSkuProductIdToInvoiceLineItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ProductId",
                schema: "pettle",
                table: "InvoiceLineItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SkuId",
                schema: "pettle",
                table: "InvoiceLineItems",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProductId",
                schema: "pettle",
                table: "InvoiceLineItems");

            migrationBuilder.DropColumn(
                name: "SkuId",
                schema: "pettle",
                table: "InvoiceLineItems");
        }
    }
}
