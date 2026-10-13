using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agendamento.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnforceQuotationCatalogTenantReference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_ServiceItems_tenant_id_Id",
                table: "ServiceItems",
                columns: new[] { "tenant_id", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_QuotationItem_tenant_id_ServiceItemId",
                table: "QuotationItem",
                columns: new[] { "tenant_id", "ServiceItemId" });

            migrationBuilder.AddForeignKey(
                name: "FK_QuotationItem_ServiceItems_tenant_id_ServiceItemId",
                table: "QuotationItem",
                columns: new[] { "tenant_id", "ServiceItemId" },
                principalTable: "ServiceItems",
                principalColumns: new[] { "tenant_id", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_QuotationItem_ServiceItems_tenant_id_ServiceItemId",
                table: "QuotationItem");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_ServiceItems_tenant_id_Id",
                table: "ServiceItems");

            migrationBuilder.DropIndex(
                name: "IX_QuotationItem_tenant_id_ServiceItemId",
                table: "QuotationItem");
        }
    }
}
