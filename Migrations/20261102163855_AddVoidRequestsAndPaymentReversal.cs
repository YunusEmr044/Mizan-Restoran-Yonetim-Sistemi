using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestoranYonetim.Migrations
{
    /// <inheritdoc />
    public partial class AddVoidRequestsAndPaymentReversal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Note",
                table: "Payments",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReversesPaymentId",
                table: "Payments",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "VoidRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Type = table.Column<int>(type: "int", nullable: false),
                    AdisyonId = table.Column<int>(type: "int", nullable: false),
                    OrderItemId = table.Column<int>(type: "int", nullable: true),
                    PaymentId = table.Column<int>(type: "int", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    RefundMethod = table.Column<int>(type: "int", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    RequestedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RequestedByUserName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    DecidedByUserName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DecidedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VoidRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VoidRequests_Adisyonlar_AdisyonId",
                        column: x => x.AdisyonId,
                        principalTable: "Adisyonlar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VoidRequests_OrderItems_OrderItemId",
                        column: x => x.OrderItemId,
                        principalTable: "OrderItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VoidRequests_Payments_PaymentId",
                        column: x => x.PaymentId,
                        principalTable: "Payments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_ReversesPaymentId",
                table: "Payments",
                column: "ReversesPaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_VoidRequests_AdisyonId",
                table: "VoidRequests",
                column: "AdisyonId");

            migrationBuilder.CreateIndex(
                name: "IX_VoidRequests_OrderItemId",
                table: "VoidRequests",
                column: "OrderItemId");

            migrationBuilder.CreateIndex(
                name: "IX_VoidRequests_PaymentId",
                table: "VoidRequests",
                column: "PaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_VoidRequests_Status_CreatedAt",
                table: "VoidRequests",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_Payments_ReversesPaymentId",
                table: "Payments",
                column: "ReversesPaymentId",
                principalTable: "Payments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Payments_Payments_ReversesPaymentId",
                table: "Payments");

            migrationBuilder.DropTable(
                name: "VoidRequests");

            migrationBuilder.DropIndex(
                name: "IX_Payments_ReversesPaymentId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "Note",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "ReversesPaymentId",
                table: "Payments");
        }
    }
}
