using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestoranYonetim.Migrations
{
    /// <inheritdoc />
    public partial class AddHomepageCms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AboutButtonText",
                table: "SiteContents",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CampaignsSectionTitle",
                table: "SiteContents",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ChefSpecialSectionSubtitle",
                table: "SiteContents",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ChefSpecialSectionTitle",
                table: "SiteContents",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FeaturedMenuSectionSubtitle",
                table: "SiteContents",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FeaturedMenuSectionTitle",
                table: "SiteContents",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FeaturesSectionSubtitle",
                table: "SiteContents",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FeaturesSectionTitle",
                table: "SiteContents",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FooterTagline",
                table: "SiteContents",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GallerySectionTitle",
                table: "SiteContents",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InstagramHandle",
                table: "SiteContents",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InstagramSectionTitle",
                table: "SiteContents",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QrSectionSubtitle",
                table: "SiteContents",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QrSectionTitle",
                table: "SiteContents",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReservationCtaButtonText",
                table: "SiteContents",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReservationCtaImageUrl",
                table: "SiteContents",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReservationCtaText",
                table: "SiteContents",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReservationCtaTitle",
                table: "SiteContents",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TestimonialsSectionTitle",
                table: "SiteContents",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WhatsAppDefaultMessage",
                table: "SiteContents",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FeaturedOrder",
                table: "MenuItems",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsFeaturedHome",
                table: "MenuItems",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ShowOnHomepage",
                table: "GalleryImages",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "HomeDisplayOrder",
                table: "Feedbacks",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ButtonText",
                table: "Campaigns",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ButtonUrl",
                table: "Campaigns",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DisplayOrder",
                table: "Campaigns",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                table: "Campaigns",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ChefSpecials",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MenuItemId = table.Column<int>(type: "int", nullable: false),
                    TitleOverride = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    DescriptionOverride = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ImageUrlOverride = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PriceOverride = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChefSpecials", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChefSpecials_MenuItems_MenuItemId",
                        column: x => x.MenuItemId,
                        principalTable: "MenuItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HeroSlides",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Subtitle = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ImageUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MobileImageUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PrimaryButtonText = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    PrimaryButtonUrl = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    SecondaryButtonText = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    SecondaryButtonUrl = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HeroSlides", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HomeFeatures",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Icon = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HomeFeatures", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HomeSections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Key = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HomeSections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HomeStats",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Value = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Label = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Icon = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HomeStats", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkingHoursDays",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DayOfWeek = table.Column<int>(type: "int", nullable: false),
                    IsClosed = table.Column<bool>(type: "bit", nullable: false),
                    OpenTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    CloseTime = table.Column<TimeSpan>(type: "time", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkingHoursDays", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MenuItems_IsFeaturedHome_FeaturedOrder",
                table: "MenuItems",
                columns: new[] { "IsFeaturedHome", "FeaturedOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_GalleryImages_ShowOnHomepage_DisplayOrder",
                table: "GalleryImages",
                columns: new[] { "ShowOnHomepage", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_ChefSpecials_IsActive_StartDate_EndDate",
                table: "ChefSpecials",
                columns: new[] { "IsActive", "StartDate", "EndDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ChefSpecials_MenuItemId",
                table: "ChefSpecials",
                column: "MenuItemId");

            migrationBuilder.CreateIndex(
                name: "IX_HeroSlides_IsActive_DisplayOrder",
                table: "HeroSlides",
                columns: new[] { "IsActive", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_HomeFeatures_IsActive_DisplayOrder",
                table: "HomeFeatures",
                columns: new[] { "IsActive", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_HomeSections_Key",
                table: "HomeSections",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HomeStats_IsActive_DisplayOrder",
                table: "HomeStats",
                columns: new[] { "IsActive", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkingHoursDays_DayOfWeek",
                table: "WorkingHoursDays",
                column: "DayOfWeek",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChefSpecials");

            migrationBuilder.DropTable(
                name: "HeroSlides");

            migrationBuilder.DropTable(
                name: "HomeFeatures");

            migrationBuilder.DropTable(
                name: "HomeSections");

            migrationBuilder.DropTable(
                name: "HomeStats");

            migrationBuilder.DropTable(
                name: "WorkingHoursDays");

            migrationBuilder.DropIndex(
                name: "IX_MenuItems_IsFeaturedHome_FeaturedOrder",
                table: "MenuItems");

            migrationBuilder.DropIndex(
                name: "IX_GalleryImages_ShowOnHomepage_DisplayOrder",
                table: "GalleryImages");

            migrationBuilder.DropColumn(
                name: "AboutButtonText",
                table: "SiteContents");

            migrationBuilder.DropColumn(
                name: "CampaignsSectionTitle",
                table: "SiteContents");

            migrationBuilder.DropColumn(
                name: "ChefSpecialSectionSubtitle",
                table: "SiteContents");

            migrationBuilder.DropColumn(
                name: "ChefSpecialSectionTitle",
                table: "SiteContents");

            migrationBuilder.DropColumn(
                name: "FeaturedMenuSectionSubtitle",
                table: "SiteContents");

            migrationBuilder.DropColumn(
                name: "FeaturedMenuSectionTitle",
                table: "SiteContents");

            migrationBuilder.DropColumn(
                name: "FeaturesSectionSubtitle",
                table: "SiteContents");

            migrationBuilder.DropColumn(
                name: "FeaturesSectionTitle",
                table: "SiteContents");

            migrationBuilder.DropColumn(
                name: "FooterTagline",
                table: "SiteContents");

            migrationBuilder.DropColumn(
                name: "GallerySectionTitle",
                table: "SiteContents");

            migrationBuilder.DropColumn(
                name: "InstagramHandle",
                table: "SiteContents");

            migrationBuilder.DropColumn(
                name: "InstagramSectionTitle",
                table: "SiteContents");

            migrationBuilder.DropColumn(
                name: "QrSectionSubtitle",
                table: "SiteContents");

            migrationBuilder.DropColumn(
                name: "QrSectionTitle",
                table: "SiteContents");

            migrationBuilder.DropColumn(
                name: "ReservationCtaButtonText",
                table: "SiteContents");

            migrationBuilder.DropColumn(
                name: "ReservationCtaImageUrl",
                table: "SiteContents");

            migrationBuilder.DropColumn(
                name: "ReservationCtaText",
                table: "SiteContents");

            migrationBuilder.DropColumn(
                name: "ReservationCtaTitle",
                table: "SiteContents");

            migrationBuilder.DropColumn(
                name: "TestimonialsSectionTitle",
                table: "SiteContents");

            migrationBuilder.DropColumn(
                name: "WhatsAppDefaultMessage",
                table: "SiteContents");

            migrationBuilder.DropColumn(
                name: "FeaturedOrder",
                table: "MenuItems");

            migrationBuilder.DropColumn(
                name: "IsFeaturedHome",
                table: "MenuItems");

            migrationBuilder.DropColumn(
                name: "ShowOnHomepage",
                table: "GalleryImages");

            migrationBuilder.DropColumn(
                name: "HomeDisplayOrder",
                table: "Feedbacks");

            migrationBuilder.DropColumn(
                name: "ButtonText",
                table: "Campaigns");

            migrationBuilder.DropColumn(
                name: "ButtonUrl",
                table: "Campaigns");

            migrationBuilder.DropColumn(
                name: "DisplayOrder",
                table: "Campaigns");

            migrationBuilder.DropColumn(
                name: "ImageUrl",
                table: "Campaigns");
        }
    }
}
