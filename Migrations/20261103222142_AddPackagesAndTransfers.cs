using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestoranYonetim.Migrations
{
    /// <inheritdoc />
    public partial class AddPackagesAndTransfers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "TableId",
                table: "Orders",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<bool>(
                name: "IsTakeaway",
                table: "OrderItems",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<int>(
                name: "TableId",
                table: "Adisyonlar",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<int>(
                name: "BranchId",
                table: "Adisyonlar",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MergedIntoAdisyonId",
                table: "Adisyonlar",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OrderType",
                table: "Adisyonlar",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Package_Address",
                table: "Adisyonlar",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Package_CourierName",
                table: "Adisyonlar",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Package_CustomerName",
                table: "Adisyonlar",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Package_CustomerPhone",
                table: "Adisyonlar",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "Package_DeliveredAt",
                table: "Adisyonlar",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "Package_DispatchedAt",
                table: "Adisyonlar",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "Package_HandedOverAt",
                table: "Adisyonlar",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Package_Note",
                table: "Adisyonlar",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "Package_PromisedAt",
                table: "Adisyonlar",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Adisyonlar_BranchId",
                table: "Adisyonlar",
                column: "BranchId");

            migrationBuilder.AddForeignKey(
                name: "FK_Adisyonlar_Branches_BranchId",
                table: "Adisyonlar",
                column: "BranchId",
                principalTable: "Branches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Adisyonlar_Branches_BranchId",
                table: "Adisyonlar");

            migrationBuilder.DropIndex(
                name: "IX_Adisyonlar_BranchId",
                table: "Adisyonlar");

            migrationBuilder.DropColumn(
                name: "IsTakeaway",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "Adisyonlar");

            migrationBuilder.DropColumn(
                name: "MergedIntoAdisyonId",
                table: "Adisyonlar");

            migrationBuilder.DropColumn(
                name: "OrderType",
                table: "Adisyonlar");

            migrationBuilder.DropColumn(
                name: "Package_Address",
                table: "Adisyonlar");

            migrationBuilder.DropColumn(
                name: "Package_CourierName",
                table: "Adisyonlar");

            migrationBuilder.DropColumn(
                name: "Package_CustomerName",
                table: "Adisyonlar");

            migrationBuilder.DropColumn(
                name: "Package_CustomerPhone",
                table: "Adisyonlar");

            migrationBuilder.DropColumn(
                name: "Package_DeliveredAt",
                table: "Adisyonlar");

            migrationBuilder.DropColumn(
                name: "Package_DispatchedAt",
                table: "Adisyonlar");

            migrationBuilder.DropColumn(
                name: "Package_HandedOverAt",
                table: "Adisyonlar");

            migrationBuilder.DropColumn(
                name: "Package_Note",
                table: "Adisyonlar");

            migrationBuilder.DropColumn(
                name: "Package_PromisedAt",
                table: "Adisyonlar");

            migrationBuilder.AlterColumn<int>(
                name: "TableId",
                table: "Orders",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "TableId",
                table: "Adisyonlar",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);
        }
    }
}
