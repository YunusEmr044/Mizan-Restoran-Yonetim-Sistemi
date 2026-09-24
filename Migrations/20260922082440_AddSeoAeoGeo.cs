using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestoranYonetim.Migrations
{
    /// <inheritdoc />
    public partial class AddSeoAeoGeo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "QrPhoneImageUrl",
                table: "SiteContents",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QrStep1Text",
                table: "SiteContents",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QrStep1Title",
                table: "SiteContents",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QrStep2Text",
                table: "SiteContents",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QrStep2Title",
                table: "SiteContents",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QrStep3Text",
                table: "SiteContents",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QrStep3Title",
                table: "SiteContents",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Faqs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Question = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Answer = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Faqs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PageSeos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PageKey = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(70)", maxLength: 70, nullable: true),
                    MetaDescription = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CanonicalUrl = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    NoIndex = table.Column<bool>(type: "bit", nullable: false),
                    NoFollow = table.Column<bool>(type: "bit", nullable: false),
                    OgTitle = table.Column<string>(type: "nvarchar(70)", maxLength: 70, nullable: true),
                    OgDescription = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    OgImageUrl = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PageSeos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SeoSettingsRows",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DefaultMetaDescription = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    DefaultOgImageUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FaviconUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IndexingEnabled = table.Column<bool>(type: "bit", nullable: false),
                    SitemapEnabled = table.Column<bool>(type: "bit", nullable: false),
                    RobotsExtraRules = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GoogleSiteVerification = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BingSiteVerification = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TwitterHandle = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    LocalBusinessType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    PriceRange = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeoSettingsRows", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Faqs_IsActive_DisplayOrder",
                table: "Faqs",
                columns: new[] { "IsActive", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_PageSeos_PageKey",
                table: "PageSeos",
                column: "PageKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Faqs");

            migrationBuilder.DropTable(
                name: "PageSeos");

            migrationBuilder.DropTable(
                name: "SeoSettingsRows");

            migrationBuilder.DropColumn(
                name: "QrPhoneImageUrl",
                table: "SiteContents");

            migrationBuilder.DropColumn(
                name: "QrStep1Text",
                table: "SiteContents");

            migrationBuilder.DropColumn(
                name: "QrStep1Title",
                table: "SiteContents");

            migrationBuilder.DropColumn(
                name: "QrStep2Text",
                table: "SiteContents");

            migrationBuilder.DropColumn(
                name: "QrStep2Title",
                table: "SiteContents");

            migrationBuilder.DropColumn(
                name: "QrStep3Text",
                table: "SiteContents");

            migrationBuilder.DropColumn(
                name: "QrStep3Title",
                table: "SiteContents");
        }
    }
}
