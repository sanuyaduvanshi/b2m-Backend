using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pettle.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPetWeightSnapshotAndReturnedQty : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "PetWeightKgSnapshot",
                schema: "pettle",
                table: "Invoices",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ReturnedQuantity",
                schema: "pettle",
                table: "InvoiceLineItems",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PetWeightKgSnapshot",
                schema: "pettle",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "ReturnedQuantity",
                schema: "pettle",
                table: "InvoiceLineItems");
        }
    }
}
