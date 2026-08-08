using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pettle.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscriptionPetScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AppliesTo",
                schema: "pettle",
                table: "SubscriptionPackages",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "PetId",
                schema: "pettle",
                table: "IssuedSubscriptions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_IssuedSubscriptions_PetId",
                schema: "pettle",
                table: "IssuedSubscriptions",
                column: "PetId");

            migrationBuilder.AddForeignKey(
                name: "FK_IssuedSubscriptions_Pets_PetId",
                schema: "pettle",
                table: "IssuedSubscriptions",
                column: "PetId",
                principalSchema: "pettle",
                principalTable: "Pets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // A vaccination course or a grooming card is bought for one animal — letting a sibling
            // consume it is wrong both medically and commercially, so those packages start out
            // per-pet. Boarding is left household-wide, because a parent genuinely may board
            // whichever animal that week; the business can switch any package either way.
            // (SubscriptionPackageType: Boarding=0, Vet=1, Grooming=2 · SubscriptionScope: PerCustomer=0, PerPet=1)
            migrationBuilder.Sql(
                """UPDATE pettle."SubscriptionPackages" SET "AppliesTo" = 1 WHERE "Type" IN (1, 2);""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_IssuedSubscriptions_Pets_PetId",
                schema: "pettle",
                table: "IssuedSubscriptions");

            migrationBuilder.DropIndex(
                name: "IX_IssuedSubscriptions_PetId",
                schema: "pettle",
                table: "IssuedSubscriptions");

            migrationBuilder.DropColumn(
                name: "AppliesTo",
                schema: "pettle",
                table: "SubscriptionPackages");

            migrationBuilder.DropColumn(
                name: "PetId",
                schema: "pettle",
                table: "IssuedSubscriptions");
        }
    }
}
