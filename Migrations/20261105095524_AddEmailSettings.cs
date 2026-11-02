using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestoranYonetim.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MailFromAddress",
                table: "SiteContents",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MailFromName",
                table: "SiteContents",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SmtpHost",
                table: "SiteContents",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SmtpPasswordProtected",
                table: "SiteContents",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SmtpPort",
                table: "SiteContents",
                type: "int",
                nullable: false,
                defaultValue: 587);

            migrationBuilder.AddColumn<bool>(
                name: "SmtpUseSsl",
                table: "SiteContents",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "SmtpUserName",
                table: "SiteContents",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MailFromAddress",
                table: "SiteContents");

            migrationBuilder.DropColumn(
                name: "MailFromName",
                table: "SiteContents");

            migrationBuilder.DropColumn(
                name: "SmtpHost",
                table: "SiteContents");

            migrationBuilder.DropColumn(
                name: "SmtpPasswordProtected",
                table: "SiteContents");

            migrationBuilder.DropColumn(
                name: "SmtpPort",
                table: "SiteContents");

            migrationBuilder.DropColumn(
                name: "SmtpUseSsl",
                table: "SiteContents");

            migrationBuilder.DropColumn(
                name: "SmtpUserName",
                table: "SiteContents");
        }
    }
}
