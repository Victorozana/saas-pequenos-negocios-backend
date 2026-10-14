using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agendamento.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnforceWorkOrderConversionIntegrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Notes",
                table: "WorkOrders",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                table: "WorkOrders",
                type: "character varying(53)",
                maxLength: 53,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "Unit",
                table: "WorkOrderItems",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "ServiceName",
                table: "WorkOrderItems",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255);

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_tenant_id_QuotationId",
                table: "WorkOrders",
                columns: new[] { "tenant_id", "QuotationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderItems_tenant_id_ServiceItemId",
                table: "WorkOrderItems",
                columns: new[] { "tenant_id", "ServiceItemId" });

            migrationBuilder.AddForeignKey(
                name: "FK_WorkOrderItems_ServiceItems_tenant_id_ServiceItemId",
                table: "WorkOrderItems",
                columns: new[] { "tenant_id", "ServiceItemId" },
                principalTable: "ServiceItems",
                principalColumns: new[] { "tenant_id", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WorkOrderItems_ServiceItems_tenant_id_ServiceItemId",
                table: "WorkOrderItems");

            migrationBuilder.DropIndex(
                name: "IX_WorkOrders_tenant_id_QuotationId",
                table: "WorkOrders");

            migrationBuilder.DropIndex(
                name: "IX_WorkOrderItems_tenant_id_ServiceItemId",
                table: "WorkOrderItems");

            migrationBuilder.AlterColumn<string>(
                name: "Notes",
                table: "WorkOrders",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                table: "WorkOrders",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(53)",
                oldMaxLength: 53);

            migrationBuilder.AlterColumn<string>(
                name: "Unit",
                table: "WorkOrderItems",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "ServiceName",
                table: "WorkOrderItems",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");
        }
    }
}
