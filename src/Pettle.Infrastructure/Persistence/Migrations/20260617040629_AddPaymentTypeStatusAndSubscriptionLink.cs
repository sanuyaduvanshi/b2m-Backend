using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pettle.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentTypeStatusAndSubscriptionLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "InvoiceId",
                schema: "pettle",
                table: "Payments",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "IssuedSubscriptionId",
                schema: "pettle",
                table: "Payments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                schema: "pettle",
                table: "Payments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Type",
                schema: "pettle",
                table: "Payments",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IssuedSubscriptionId",
                schema: "pettle",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "Status",
                schema: "pettle",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "Type",
                schema: "pettle",
                table: "Payments");

            migrationBuilder.AlterColumn<Guid>(
                name: "InvoiceId",
                schema: "pettle",
                table: "Payments",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
