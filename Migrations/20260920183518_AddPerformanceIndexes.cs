using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestoranYonetim.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Adisyonlar_TableId",
                table: "Adisyonlar");

            migrationBuilder.CreateIndex(
                name: "IX_WaiterCalls_Resolved",
                table: "WaiterCalls",
                column: "Resolved");

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_Date",
                table: "Reservations",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_CreatedAt",
                table: "Payments",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_IsDraft_Status",
                table: "Orders",
                columns: new[] { "IsDraft", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_SentAt",
                table: "Orders",
                column: "SentAt");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_CreatedAt",
                table: "Notifications",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_IsRead",
                table: "Notifications",
                column: "IsRead");

            migrationBuilder.CreateIndex(
                name: "IX_DiscountRequests_Status",
                table: "DiscountRequests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_Phone",
                table: "Customers",
                column: "Phone");

            migrationBuilder.CreateIndex(
                name: "IX_CashShifts_ClosedAt",
                table: "CashShifts",
                column: "ClosedAt",
                filter: "[ClosedAt] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Adisyonlar_Status_ClosedAt",
                table: "Adisyonlar",
                columns: new[] { "Status", "ClosedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Adisyonlar_TableId_Status",
                table: "Adisyonlar",
                columns: new[] { "TableId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WaiterCalls_Resolved",
                table: "WaiterCalls");

            migrationBuilder.DropIndex(
                name: "IX_Reservations_Date",
                table: "Reservations");

            migrationBuilder.DropIndex(
                name: "IX_Payments_CreatedAt",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Orders_IsDraft_Status",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_SentAt",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_CreatedAt",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_IsRead",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_DiscountRequests_Status",
                table: "DiscountRequests");

            migrationBuilder.DropIndex(
                name: "IX_Customers_Phone",
                table: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_CashShifts_ClosedAt",
                table: "CashShifts");

            migrationBuilder.DropIndex(
                name: "IX_Adisyonlar_Status_ClosedAt",
                table: "Adisyonlar");

            migrationBuilder.DropIndex(
                name: "IX_Adisyonlar_TableId_Status",
                table: "Adisyonlar");

            migrationBuilder.CreateIndex(
                name: "IX_Adisyonlar_TableId",
                table: "Adisyonlar",
                column: "TableId");
        }
    }
}
