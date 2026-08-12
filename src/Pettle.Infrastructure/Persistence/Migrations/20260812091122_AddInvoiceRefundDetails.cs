using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pettle.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceRefundDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RefundReason",
                schema: "pettle",
                table: "Invoices",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RefundedAmount",
                schema: "pettle",
                table: "Invoices",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RefundedAt",
                schema: "pettle",
                table: "Invoices",
                type: "timestamp with time zone",
                nullable: true);

            // Data fix, not just schema: every invoice ever refunded before this migration was left
            // with Due = Revenue - Paid, which re-inflates back up to the refunded amount (a fully
            // refunded ₹200 sale shows "Due ₹200" in red right next to its "Refunded" badge) — and
            // the same wrong Due fed every Outstanding total downstream (Reports, the Invoices list
            // KPI, Bookings' balance column), since they all just sum this field. A refunded invoice
            // is closed, not awaiting collection, so Due goes to 0. Cancelled invoices get the same
            // treatment for the same reason, even though nothing in the app currently sets that status.
            migrationBuilder.Sql(@"UPDATE pettle.""Invoices"" SET ""Due"" = 0 WHERE ""PaymentStatus"" IN (3, 4);");

            // Best-effort backfill of the new structured fields from the free-text Notes trail
            // ("Refund ₹200.00: reason") so already-refunded invoices get the same clear banner as
            // new ones, instead of showing it only going forward. Picks the first "Refund ₹" entry in
            // Notes — the common case is one refund per invoice; a rare invoice refunded more than
            // once keeps Due correctly at 0 either way, it just won't show a summed refund amount here.
            migrationBuilder.Sql(@"
                UPDATE pettle.""Invoices""
                SET ""RefundedAmount"" = (regexp_match(""Notes"", 'Refund ₹([0-9]+\.?[0-9]*):'))[1]::numeric,
                    ""RefundedAt"" = COALESCE(""UpdatedAt"", ""CreatedAt""),
                    ""RefundReason"" = NULLIF(trim(both ' ' from regexp_replace(
                        (regexp_match(""Notes"", 'Refund ₹[0-9]+\.?[0-9]*:\s*(.*)'))[1],
                        '\s\|\s.*$', '')), '')
                WHERE ""PaymentStatus"" = 3 AND ""Notes"" IS NOT NULL AND ""Notes"" ~ 'Refund ₹';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RefundReason",
                schema: "pettle",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "RefundedAmount",
                schema: "pettle",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "RefundedAt",
                schema: "pettle",
                table: "Invoices");
        }
    }
}
