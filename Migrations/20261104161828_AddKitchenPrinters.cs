using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestoranYonetim.Migrations
{
    /// <inheritdoc />
    public partial class AddKitchenPrinters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "KitchenPrinters",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    BranchId = table.Column<int>(type: "int", nullable: false),
                    Department = table.Column<int>(type: "int", nullable: false),
                    Connection = table.Column<int>(type: "int", nullable: false),
                    Host = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Port = table.Column<int>(type: "int", nullable: false),
                    PaperWidthMm = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KitchenPrinters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KitchenPrinters_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KitchenPrinters_BranchId",
                table: "KitchenPrinters",
                column: "BranchId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KitchenPrinters");
        }
    }
}
