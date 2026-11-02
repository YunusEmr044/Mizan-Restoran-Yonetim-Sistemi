using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestoranYonetim.Migrations
{
    /// <inheritdoc />
    public partial class SplitPrepScreenPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Mutfak ve bar personeli artık yalnızca kendi ekranının iznini alır; yeni izin satırlarını DbSeeder ekler.
            migrationBuilder.Sql("UPDATE RolePermissions SET IsEnabled = 0 WHERE Permission = 'orders.view' AND RoleName IN ('MutfakPersoneli', 'BarPersoneli')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE RolePermissions SET IsEnabled = 1 WHERE Permission = 'orders.view' AND RoleName IN ('MutfakPersoneli', 'BarPersoneli')");
            migrationBuilder.Sql("DELETE FROM RolePermissions WHERE Permission IN ('orders.kitchen', 'orders.bar')");
        }
    }
}
