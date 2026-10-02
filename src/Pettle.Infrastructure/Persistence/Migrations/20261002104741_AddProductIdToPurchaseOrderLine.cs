using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pettle.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProductIdToPurchaseOrderLine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ProductId",
                schema: "pettle",
                table: "PurchaseOrderLines",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProductId",
                schema: "pettle",
                table: "PurchaseOrderLines");
        }
    }
}
