using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pettle.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditActorRoleModuleSummary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ActorRoleName",
                schema: "pettle",
                table: "AuditEntries",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Module",
                schema: "pettle",
                table: "AuditEntries",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Summary",
                schema: "pettle",
                table: "AuditEntries",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActorRoleName",
                schema: "pettle",
                table: "AuditEntries");

            migrationBuilder.DropColumn(
                name: "Module",
                schema: "pettle",
                table: "AuditEntries");

            migrationBuilder.DropColumn(
                name: "Summary",
                schema: "pettle",
                table: "AuditEntries");
        }
    }
}
