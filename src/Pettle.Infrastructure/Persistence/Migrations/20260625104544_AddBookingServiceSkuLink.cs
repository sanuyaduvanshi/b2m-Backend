using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pettle.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingServiceSkuLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SkuId",
                schema: "pettle",
                table: "BookingServices",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SkuQuantity",
                schema: "pettle",
                table: "BookingServices",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SkuId",
                schema: "pettle",
                table: "BookingServices");

            migrationBuilder.DropColumn(
                name: "SkuQuantity",
                schema: "pettle",
                table: "BookingServices");
        }
    }
}
