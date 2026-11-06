using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agendamento.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ScheduleWorkOrderTeams : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Appointments_WorkOrders_WorkOrderId",
                table: "Appointments");

            migrationBuilder.DropIndex(
                name: "IX_WorkOrders_tenant_id_QuotationId",
                table: "WorkOrders");

            migrationBuilder.DropIndex(
                name: "IX_Appointments_WorkOrderId",
                table: "Appointments");

            migrationBuilder.AddColumn<string>(
                name: "RequestHash",
                table: "WorkOrders",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RequestKey",
                table: "WorkOrders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Sequence",
                table: "WorkOrders",
                type: "integer",
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_WorkOrders_tenant_id_Id",
                table: "WorkOrders",
                columns: new[] { "tenant_id", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_tenant_memberships_tenant_id_UserId",
                table: "tenant_memberships",
                columns: new[] { "tenant_id", "UserId" });

            migrationBuilder.CreateTable(
                name: "WorkOrderParticipants",
                columns: table => new
                {
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkOrderParticipants", x => new { x.tenant_id, x.WorkOrderId, x.UserId });
                    table.ForeignKey(
                        name: "FK_WorkOrderParticipants_WorkOrders_tenant_id_WorkOrderId",
                        columns: x => new { x.tenant_id, x.WorkOrderId },
                        principalTable: "WorkOrders",
                        principalColumns: new[] { "tenant_id", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkOrderParticipants_tenant_memberships_tenant_id_UserId",
                        columns: x => new { x.tenant_id, x.UserId },
                        principalTable: "tenant_memberships",
                        principalColumns: new[] { "tenant_id", "UserId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkOrderParticipants_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.Sql("""
                ALTER TABLE "WorkOrderParticipants" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE "WorkOrderParticipants" FORCE ROW LEVEL SECURITY;
                GRANT SELECT, INSERT, UPDATE, DELETE ON "WorkOrderParticipants" TO agendamento_app_user;
                CREATE POLICY tenant_isolation ON "WorkOrderParticipants" TO agendamento_app_user
                  USING (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid)
                  WITH CHECK (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid);
                CREATE POLICY tenant_guard ON "WorkOrderParticipants" AS RESTRICTIVE TO agendamento_app_user
                  USING (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid)
                  WITH CHECK (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid);
                """);

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_tenant_id_QuotationId",
                table: "WorkOrders",
                columns: new[] { "tenant_id", "QuotationId" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_tenant_id_RequestKey",
                table: "WorkOrders",
                columns: new[] { "tenant_id", "RequestKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_tenant_id_Sequence",
                table: "WorkOrders",
                columns: new[] { "tenant_id", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_tenant_id_WorkOrderId",
                table: "Appointments",
                columns: new[] { "tenant_id", "WorkOrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderParticipants_tenant_id",
                table: "WorkOrderParticipants",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderParticipants_tenant_id_UserId",
                table: "WorkOrderParticipants",
                columns: new[] { "tenant_id", "UserId" });

            migrationBuilder.AddForeignKey(
                name: "FK_Appointments_WorkOrders_tenant_id_WorkOrderId",
                table: "Appointments",
                columns: new[] { "tenant_id", "WorkOrderId" },
                principalTable: "WorkOrders",
                principalColumns: new[] { "tenant_id", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Appointments_WorkOrders_tenant_id_WorkOrderId",
                table: "Appointments");

            migrationBuilder.DropTable(
                name: "WorkOrderParticipants");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_WorkOrders_tenant_id_Id",
                table: "WorkOrders");

            migrationBuilder.DropIndex(
                name: "IX_WorkOrders_tenant_id_QuotationId",
                table: "WorkOrders");

            migrationBuilder.DropIndex(
                name: "IX_WorkOrders_tenant_id_RequestKey",
                table: "WorkOrders");

            migrationBuilder.DropIndex(
                name: "IX_WorkOrders_tenant_id_Sequence",
                table: "WorkOrders");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_tenant_memberships_tenant_id_UserId",
                table: "tenant_memberships");

            migrationBuilder.DropIndex(
                name: "IX_Appointments_tenant_id_WorkOrderId",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "RequestHash",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "RequestKey",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "Sequence",
                table: "WorkOrders");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_tenant_id_QuotationId",
                table: "WorkOrders",
                columns: new[] { "tenant_id", "QuotationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_WorkOrderId",
                table: "Appointments",
                column: "WorkOrderId");

            migrationBuilder.AddForeignKey(
                name: "FK_Appointments_WorkOrders_WorkOrderId",
                table: "Appointments",
                column: "WorkOrderId",
                principalTable: "WorkOrders",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
