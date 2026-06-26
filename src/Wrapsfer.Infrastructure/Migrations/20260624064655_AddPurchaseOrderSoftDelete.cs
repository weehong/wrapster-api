using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wrapsfer.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPurchaseOrderSoftDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrders_TenantId_PoNumber",
                table: "PurchaseOrders");

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "PurchaseOrders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "PurchaseOrders",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_TenantId_PoNumber",
                table: "PurchaseOrders",
                columns: new[] { "TenantId", "PoNumber" },
                unique: true,
                filter: "\"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The non-filtered unique index restored below cannot represent soft-deleted rows that
            // reused a (TenantId, PoNumber) of a live order. Rather than silently purging business
            // data on rollback, fail fast with an actionable message so an operator decides how to
            // reconcile the soft-deleted orders before reverting.
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM "PurchaseOrders" WHERE "IsDeleted" = true) THEN
                        RAISE EXCEPTION 'Cannot roll back AddPurchaseOrderSoftDelete: soft-deleted PurchaseOrders exist. Hard-delete or restore them before reverting, since the original unique index cannot represent soft-deleted rows.';
                    END IF;
                END $$;
                """);

            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrders_TenantId_PoNumber",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "PurchaseOrders");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_TenantId_PoNumber",
                table: "PurchaseOrders",
                columns: new[] { "TenantId", "PoNumber" },
                unique: true);
        }
    }
}
