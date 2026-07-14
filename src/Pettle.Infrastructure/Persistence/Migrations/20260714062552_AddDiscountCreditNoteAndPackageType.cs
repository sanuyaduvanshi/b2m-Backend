using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pettle.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDiscountCreditNoteAndPackageType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Type",
                schema: "pettle",
                table: "SubscriptionPackages",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "AdditionalChargesReason",
                schema: "pettle",
                table: "Invoices",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RemainingCreditAmount",
                schema: "pettle",
                table: "Invoices",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountPercent",
                schema: "pettle",
                table: "Bookings",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "GrossBillingAmount",
                schema: "pettle",
                table: "Bookings",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Type",
                schema: "pettle",
                table: "SubscriptionPackages");

            migrationBuilder.DropColumn(
                name: "AdditionalChargesReason",
                schema: "pettle",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "RemainingCreditAmount",
                schema: "pettle",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "DiscountPercent",
                schema: "pettle",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "GrossBillingAmount",
                schema: "pettle",
                table: "Bookings");
        }
    }
}
