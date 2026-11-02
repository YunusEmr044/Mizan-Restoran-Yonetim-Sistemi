using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestoranYonetim.Migrations
{
    /// <inheritdoc />
    public partial class AddLegalPagesAndAnalytics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AnalyticsId",
                table: "SiteContents",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AnalyticsProvider",
                table: "SiteContents",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "AnalyticsScriptUrl",
                table: "SiteContents",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LegalPages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Slug = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsPublished = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LegalPages", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LegalPages_Slug",
                table: "LegalPages",
                column: "Slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LegalPages");

            migrationBuilder.DropColumn(
                name: "AnalyticsId",
                table: "SiteContents");

            migrationBuilder.DropColumn(
                name: "AnalyticsProvider",
                table: "SiteContents");

            migrationBuilder.DropColumn(
                name: "AnalyticsScriptUrl",
                table: "SiteContents");
        }
    }
}
