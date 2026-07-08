using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wrapsfer.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddShopeeProductLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ShopeeProductLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(63)", maxLength: 63, nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    ShopeeItemId = table.Column<long>(type: "bigint", nullable: false),
                    ShopeeModelId = table.Column<long>(type: "bigint", nullable: false),
                    ShopeeItemName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ShopeeModelName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ShopeeItemSku = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    LastSyncedQuantity = table.Column<int>(type: "integer", nullable: true),
                    LastSyncedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastSyncAttemptedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SyncFailureCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    NextSyncEligibleAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastSyncError = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    LinkedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShopeeProductLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShopeeProductLinks_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ShopeeProductLinks_LastSyncAttemptedAt",
                table: "ShopeeProductLinks",
                column: "LastSyncAttemptedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ShopeeProductLinks_NextSyncEligibleAt",
                table: "ShopeeProductLinks",
                column: "NextSyncEligibleAt");

            migrationBuilder.CreateIndex(
                name: "IX_ShopeeProductLinks_ProductId",
                table: "ShopeeProductLinks",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ShopeeProductLinks_TenantId_ProductId",
                table: "ShopeeProductLinks",
                columns: new[] { "TenantId", "ProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShopeeProductLinks_TenantId_ShopeeItemId_ShopeeModelId",
                table: "ShopeeProductLinks",
                columns: new[] { "TenantId", "ShopeeItemId", "ShopeeModelId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ShopeeProductLinks");
        }
    }
}
