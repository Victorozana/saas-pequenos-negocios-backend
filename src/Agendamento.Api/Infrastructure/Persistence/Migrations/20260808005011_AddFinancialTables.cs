using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agendamento.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFinancialTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PayableTitles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    OriginalAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayableTitles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReceivableTitles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: true),
                    QuotationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Description = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    OriginalAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReceivableTitles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReceivableTitles_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReceivableTitles_Quotations_QuotationId",
                        column: x => x.QuotationId,
                        principalTable: "Quotations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "PayablePayments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Method = table.Column<string>(type: "text", nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PayableTitleId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayablePayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayablePayments_PayableTitles_PayableTitleId",
                        column: x => x.PayableTitleId,
                        principalTable: "PayableTitles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReceivablePayments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Method = table.Column<string>(type: "text", nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ReceivableTitleId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReceivablePayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReceivablePayments_ReceivableTitles_ReceivableTitleId",
                        column: x => x.ReceivableTitleId,
                        principalTable: "ReceivableTitles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PayablePayments_PayableTitleId",
                table: "PayablePayments",
                column: "PayableTitleId");

            migrationBuilder.CreateIndex(
                name: "IX_PayableTitles_TenantId_DueDate_Status",
                table: "PayableTitles",
                columns: new[] { "TenantId", "DueDate", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ReceivablePayments_ReceivableTitleId",
                table: "ReceivablePayments",
                column: "ReceivableTitleId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceivableTitles_CustomerId",
                table: "ReceivableTitles",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceivableTitles_QuotationId",
                table: "ReceivableTitles",
                column: "QuotationId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceivableTitles_TenantId_DueDate_Status",
                table: "ReceivableTitles",
                columns: new[] { "TenantId", "DueDate", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PayablePayments");

            migrationBuilder.DropTable(
                name: "ReceivablePayments");

            migrationBuilder.DropTable(
                name: "PayableTitles");

            migrationBuilder.DropTable(
                name: "ReceivableTitles");
        }
    }
}
