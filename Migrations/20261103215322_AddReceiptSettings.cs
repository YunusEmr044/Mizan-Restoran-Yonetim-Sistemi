using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestoranYonetim.Migrations
{
    /// <inheritdoc />
    public partial class AddReceiptSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReceiptFontSize",
                table: "SiteContents",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "normal");

            migrationBuilder.AddColumn<string>(
                name: "ReceiptFooterText",
                table: "SiteContents",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReceiptPaperSize",
                table: "SiteContents",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "80mm");

            migrationBuilder.AddColumn<bool>(
                name: "ReceiptShowAddress",
                table: "SiteContents",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "ReceiptShowPayments",
                table: "SiteContents",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "ReceiptShowPhone",
                table: "SiteContents",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "ReceiptShowWaiter",
                table: "SiteContents",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "ReceiptTaxInfo",
                table: "SiteContents",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReceiptFontSize",
                table: "SiteContents");

            migrationBuilder.DropColumn(
                name: "ReceiptFooterText",
                table: "SiteContents");

            migrationBuilder.DropColumn(
                name: "ReceiptPaperSize",
                table: "SiteContents");

            migrationBuilder.DropColumn(
                name: "ReceiptShowAddress",
                table: "SiteContents");

            migrationBuilder.DropColumn(
                name: "ReceiptShowPayments",
                table: "SiteContents");

            migrationBuilder.DropColumn(
                name: "ReceiptShowPhone",
                table: "SiteContents");

            migrationBuilder.DropColumn(
                name: "ReceiptShowWaiter",
                table: "SiteContents");

            migrationBuilder.DropColumn(
                name: "ReceiptTaxInfo",
                table: "SiteContents");
        }
    }
}
