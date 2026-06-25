using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pettle.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWalkInGuestBooking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "PetId",
                schema: "pettle",
                table: "BookingServices",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "PetNameSnapshot",
                schema: "pettle",
                table: "BookingServices",
                type: "text",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "PetParentId",
                schema: "pettle",
                table: "Bookings",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "GuestName",
                schema: "pettle",
                table: "Bookings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuestPhone",
                schema: "pettle",
                table: "Bookings",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PetNameSnapshot",
                schema: "pettle",
                table: "BookingServices");

            migrationBuilder.DropColumn(
                name: "GuestName",
                schema: "pettle",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "GuestPhone",
                schema: "pettle",
                table: "Bookings");

            migrationBuilder.AlterColumn<Guid>(
                name: "PetId",
                schema: "pettle",
                table: "BookingServices",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "PetParentId",
                schema: "pettle",
                table: "Bookings",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
