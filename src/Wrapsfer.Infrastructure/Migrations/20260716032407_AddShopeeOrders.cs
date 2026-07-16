using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wrapsfer.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddShopeeOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ShopeeOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(63)", maxLength: 63, nullable: false),
                    OrderSn = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Region = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    ShopeeStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    BuyerUsername = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    RecipientName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    RecipientPhone = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    RecipientAddress = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    CodAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    ShippingCarrier = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ShipByDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TrackingNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    WaybillId = table.Column<Guid>(type: "uuid", nullable: true),
                    ShipmentArrangedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ShipmentArrangedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    LabelStorageKey = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    LabelPrintedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastSyncedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastShipError = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShopeeOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShopeeOrders_Waybills_WaybillId",
                        column: x => x.WaybillId,
                        principalTable: "Waybills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ShopeeWebhookEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShopId = table.Column<long>(type: "bigint", nullable: false),
                    Code = table.Column<int>(type: "integer", nullable: false),
                    MessageKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Payload = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ReceivedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProcessedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    NextAttemptAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Error = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShopeeWebhookEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ShopeeOrderItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(63)", maxLength: 63, nullable: false),
                    ShopeeOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ShopeeItemId = table.Column<long>(type: "bigint", nullable: false),
                    ShopeeModelId = table.Column<long>(type: "bigint", nullable: false),
                    ItemName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ModelName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ItemSku = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShopeeOrderItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShopeeOrderItems_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ShopeeOrderItems_ShopeeOrders_ShopeeOrderId",
                        column: x => x.ShopeeOrderId,
                        principalTable: "ShopeeOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ShopeeOrderItems_ProductId",
                table: "ShopeeOrderItems",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ShopeeOrderItems_ShopeeOrderId",
                table: "ShopeeOrderItems",
                column: "ShopeeOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ShopeeOrderItems_TenantId_ShopeeItemId_ShopeeModelId",
                table: "ShopeeOrderItems",
                columns: new[] { "TenantId", "ShopeeItemId", "ShopeeModelId" });

            migrationBuilder.CreateIndex(
                name: "IX_ShopeeOrders_ShipmentArrangedAt",
                table: "ShopeeOrders",
                column: "ShipmentArrangedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ShopeeOrders_Status",
                table: "ShopeeOrders",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ShopeeOrders_TenantId_OrderSn",
                table: "ShopeeOrders",
                columns: new[] { "TenantId", "OrderSn" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShopeeOrders_TenantId_Status",
                table: "ShopeeOrders",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ShopeeOrders_WaybillId",
                table: "ShopeeOrders",
                column: "WaybillId");

            migrationBuilder.CreateIndex(
                name: "IX_ShopeeWebhookEvents_MessageKey",
                table: "ShopeeWebhookEvents",
                column: "MessageKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShopeeWebhookEvents_ReceivedAtUtc",
                table: "ShopeeWebhookEvents",
                column: "ReceivedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ShopeeWebhookEvents_Status_NextAttemptAt",
                table: "ShopeeWebhookEvents",
                columns: new[] { "Status", "NextAttemptAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ShopeeOrderItems");

            migrationBuilder.DropTable(
                name: "ShopeeWebhookEvents");

            migrationBuilder.DropTable(
                name: "ShopeeOrders");
        }
    }
}
