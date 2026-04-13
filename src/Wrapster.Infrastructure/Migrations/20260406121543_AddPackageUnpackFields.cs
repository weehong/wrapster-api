using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wrapster.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPackageUnpackFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "UnpackQuantityPerPackage",
                table: "Products",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UnpackTargetProductId",
                table: "Products",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Products_UnpackTargetProductId",
                table: "Products",
                column: "UnpackTargetProductId");

            migrationBuilder.AddForeignKey(
                name: "FK_Products_Products_UnpackTargetProductId",
                table: "Products",
                column: "UnpackTargetProductId",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Products_Products_UnpackTargetProductId",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_UnpackTargetProductId",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "UnpackQuantityPerPackage",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "UnpackTargetProductId",
                table: "Products");
        }
    }
}
