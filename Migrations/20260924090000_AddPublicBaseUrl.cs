using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestoranYonetim.Migrations
{
    /// <inheritdoc />
    public partial class AddPublicBaseUrl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PublicBaseUrl",
                table: "SiteContents",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PublicBaseUrl",
                table: "SiteContents");
        }
    }
}
