using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wrapsfer.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddShopeeShopConnections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ShopeeShopConnections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(63)", maxLength: 63, nullable: false),
                    ShopId = table.Column<long>(type: "bigint", nullable: false),
                    ShopName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Region = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    AccessToken = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    RefreshToken = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    AccessTokenExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RefreshTokenExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LinkedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LinkedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShopeeShopConnections", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ShopeeShopConnections_AccessTokenExpiresAt",
                table: "ShopeeShopConnections",
                column: "AccessTokenExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_ShopeeShopConnections_TenantId",
                table: "ShopeeShopConnections",
                column: "TenantId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ShopeeShopConnections");
        }
    }
}
