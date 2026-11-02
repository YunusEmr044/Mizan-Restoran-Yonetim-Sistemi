using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestoranYonetim.Migrations
{
    /// <inheritdoc />
    public partial class AddFiscal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "VatRate",
                table: "OrderItems",
                type: "int",
                nullable: false,
                defaultValue: 10);

            migrationBuilder.AddColumn<int>(
                name: "VatRate",
                table: "MenuItems",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VatRate",
                table: "Categories",
                type: "int",
                nullable: false,
                defaultValue: 10);

            migrationBuilder.CreateTable(
                name: "FiscalDevices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    AdapterKey = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SettingsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FiscalDevices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FiscalDevices_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FiscalReceipts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AdisyonId = table.Column<int>(type: "int", nullable: false),
                    FiscalDeviceId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ReceiptNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ZNo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Total = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    LastError = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsSimulated = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PrintedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PrintedByUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FiscalReceipts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FiscalReceipts_Adisyonlar_AdisyonId",
                        column: x => x.AdisyonId,
                        principalTable: "Adisyonlar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FiscalReceipts_FiscalDevices_FiscalDeviceId",
                        column: x => x.FiscalDeviceId,
                        principalTable: "FiscalDevices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FiscalDevices_BranchId",
                table: "FiscalDevices",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_FiscalReceipts_AdisyonId",
                table: "FiscalReceipts",
                column: "AdisyonId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FiscalReceipts_FiscalDeviceId",
                table: "FiscalReceipts",
                column: "FiscalDeviceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FiscalReceipts");

            migrationBuilder.DropTable(
                name: "FiscalDevices");

            migrationBuilder.DropColumn(
                name: "VatRate",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "VatRate",
                table: "MenuItems");

            migrationBuilder.DropColumn(
                name: "VatRate",
                table: "Categories");
        }
    }
}
