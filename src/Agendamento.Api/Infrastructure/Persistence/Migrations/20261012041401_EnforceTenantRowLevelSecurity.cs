using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agendamento.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnforceTenantRowLevelSecurity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_tenant_fiscal_profiles_tenants_TenantId",
                table: "tenant_fiscal_profiles");

            migrationBuilder.DropForeignKey(
                name: "FK_tenant_memberships_tenants_TenantId",
                table: "tenant_memberships");

            migrationBuilder.RenameColumn(
                name: "TenantId",
                table: "WorkOrders",
                newName: "tenant_id");

            migrationBuilder.RenameIndex(
                name: "IX_WorkOrders_TenantId",
                table: "WorkOrders",
                newName: "IX_WorkOrders_tenant_id");

            migrationBuilder.RenameColumn(
                name: "TenantId",
                table: "TenantSubscriptions",
                newName: "tenant_id");

            migrationBuilder.RenameIndex(
                name: "IX_TenantSubscriptions_TenantId",
                table: "TenantSubscriptions",
                newName: "IX_TenantSubscriptions_tenant_id");

            migrationBuilder.RenameColumn(
                name: "TenantId",
                table: "tenant_memberships",
                newName: "tenant_id");

            migrationBuilder.RenameIndex(
                name: "IX_tenant_memberships_TenantId_UserId",
                table: "tenant_memberships",
                newName: "IX_tenant_memberships_tenant_id_UserId");

            migrationBuilder.RenameColumn(
                name: "TenantId",
                table: "tenant_fiscal_profiles",
                newName: "tenant_id");

            migrationBuilder.RenameColumn(
                name: "TenantId",
                table: "team_invitations",
                newName: "tenant_id");

            migrationBuilder.RenameColumn(
                name: "TenantId",
                table: "ServiceItems",
                newName: "tenant_id");

            migrationBuilder.RenameIndex(
                name: "IX_ServiceItems_TenantId_IsActive_Name",
                table: "ServiceItems",
                newName: "IX_ServiceItems_tenant_id_IsActive_Name");

            migrationBuilder.RenameColumn(
                name: "TenantId",
                table: "ReceivableTitles",
                newName: "tenant_id");

            migrationBuilder.RenameIndex(
                name: "IX_ReceivableTitles_TenantId_DueDate_Status",
                table: "ReceivableTitles",
                newName: "IX_ReceivableTitles_tenant_id_DueDate_Status");

            migrationBuilder.RenameColumn(
                name: "TenantId",
                table: "Quotations",
                newName: "tenant_id");

            migrationBuilder.RenameIndex(
                name: "IX_Quotations_TenantId_Status",
                table: "Quotations",
                newName: "IX_Quotations_tenant_id_Status");

            migrationBuilder.RenameIndex(
                name: "IX_Quotations_TenantId_CustomerId",
                table: "Quotations",
                newName: "IX_Quotations_tenant_id_CustomerId");

            migrationBuilder.RenameIndex(
                name: "IX_Quotations_TenantId_Code",
                table: "Quotations",
                newName: "IX_Quotations_tenant_id_Code");

            migrationBuilder.RenameColumn(
                name: "TenantId",
                table: "PayableTitles",
                newName: "tenant_id");

            migrationBuilder.RenameIndex(
                name: "IX_PayableTitles_TenantId_DueDate_Status",
                table: "PayableTitles",
                newName: "IX_PayableTitles_tenant_id_DueDate_Status");

            migrationBuilder.RenameColumn(
                name: "TenantId",
                table: "NotificationMessages",
                newName: "tenant_id");

            migrationBuilder.RenameIndex(
                name: "IX_NotificationMessages_TenantId_Status",
                table: "NotificationMessages",
                newName: "IX_NotificationMessages_tenant_id_Status");

            migrationBuilder.RenameColumn(
                name: "TenantId",
                table: "Customers",
                newName: "tenant_id");

            migrationBuilder.RenameIndex(
                name: "IX_Customers_TenantId_Phone",
                table: "Customers",
                newName: "IX_Customers_tenant_id_Phone");

            migrationBuilder.RenameIndex(
                name: "IX_Customers_TenantId_Name",
                table: "Customers",
                newName: "IX_Customers_tenant_id_Name");

            migrationBuilder.RenameIndex(
                name: "IX_Customers_TenantId_Id",
                table: "Customers",
                newName: "IX_Customers_tenant_id_Id");

            migrationBuilder.RenameColumn(
                name: "TenantId",
                table: "Appointments",
                newName: "tenant_id");

            migrationBuilder.RenameIndex(
                name: "IX_Appointments_TenantId_StartTime",
                table: "Appointments",
                newName: "IX_Appointments_tenant_id_StartTime");

            migrationBuilder.RenameIndex(
                name: "IX_Appointments_TenantId_AssignedToUserId",
                table: "Appointments",
                newName: "IX_Appointments_tenant_id_AssignedToUserId");

            migrationBuilder.RenameIndex(
                name: "IX_Appointments_TenantId",
                table: "Appointments",
                newName: "IX_Appointments_tenant_id");

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "WorkOrderItems",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "ReceivablePayments",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "QuotationItem",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                table: "PayablePayments",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.Sql("""
UPDATE "WorkOrderItems" c SET tenant_id = p.tenant_id FROM "WorkOrders" p WHERE c."WorkOrderId" = p."Id";
ALTER TABLE "WorkOrderItems" ALTER COLUMN tenant_id DROP DEFAULT;
UPDATE "QuotationItem" c SET tenant_id = p.tenant_id FROM "Quotations" p WHERE c."QuotationId" = p."Id";
ALTER TABLE "QuotationItem" ALTER COLUMN tenant_id DROP DEFAULT;
UPDATE "ReceivablePayments" c SET tenant_id = p.tenant_id FROM "ReceivableTitles" p WHERE c."ReceivableTitleId" = p."Id";
ALTER TABLE "ReceivablePayments" ALTER COLUMN tenant_id DROP DEFAULT;
UPDATE "PayablePayments" c SET tenant_id = p.tenant_id FROM "PayableTitles" p WHERE c."PayableTitleId" = p."Id";
ALTER TABLE "PayablePayments" ALTER COLUMN tenant_id DROP DEFAULT;
""");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderItems_tenant_id",
                table: "WorkOrderItems",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_tenant_memberships_tenant_id",
                table: "tenant_memberships",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_tenant_fiscal_profiles_tenant_id",
                table: "tenant_fiscal_profiles",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_team_invitations_tenant_id",
                table: "team_invitations",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceItems_tenant_id",
                table: "ServiceItems",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_ReceivableTitles_tenant_id",
                table: "ReceivableTitles",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_ReceivablePayments_tenant_id",
                table: "ReceivablePayments",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_Quotations_tenant_id",
                table: "Quotations",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_QuotationItem_tenant_id",
                table: "QuotationItem",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_PayableTitles_tenant_id",
                table: "PayableTitles",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_PayablePayments_tenant_id",
                table: "PayablePayments",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationMessages_tenant_id",
                table: "NotificationMessages",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_tenant_id",
                table: "Customers",
                column: "tenant_id");

            migrationBuilder.AddForeignKey(
                name: "FK_Appointments_tenants_tenant_id",
                table: "Appointments",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Customers_tenants_tenant_id",
                table: "Customers",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_NotificationMessages_tenants_tenant_id",
                table: "NotificationMessages",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PayablePayments_tenants_tenant_id",
                table: "PayablePayments",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PayableTitles_tenants_tenant_id",
                table: "PayableTitles",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_QuotationItem_tenants_tenant_id",
                table: "QuotationItem",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Quotations_tenants_tenant_id",
                table: "Quotations",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ReceivablePayments_tenants_tenant_id",
                table: "ReceivablePayments",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ReceivableTitles_tenants_tenant_id",
                table: "ReceivableTitles",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ServiceItems_tenants_tenant_id",
                table: "ServiceItems",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_team_invitations_tenants_tenant_id",
                table: "team_invitations",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_tenant_fiscal_profiles_tenants_tenant_id",
                table: "tenant_fiscal_profiles",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_tenant_memberships_tenants_tenant_id",
                table: "tenant_memberships",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TenantSubscriptions_tenants_tenant_id",
                table: "TenantSubscriptions",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkOrderItems_tenants_tenant_id",
                table: "WorkOrderItems",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkOrders_tenants_tenant_id",
                table: "WorkOrders",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
            using var stream = typeof(EnforceTenantRowLevelSecurity).Assembly.GetManifestResourceStream("Agendamento.Api.TenantRls.sql")!;
            using var reader = new System.IO.StreamReader(stream);
            migrationBuilder.Sql(reader.ReadToEnd());
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
DO $$ DECLARE r record; BEGIN
 FOR r IN SELECT conrelid::regclass AS tbl, conname FROM pg_constraint WHERE conname LIKE 'FK_rls_%'
 LOOP EXECUTE format('ALTER TABLE %s DROP CONSTRAINT %I', r.tbl, r.conname); END LOOP;
 FOR r IN SELECT indexrelid::regclass AS idx FROM pg_index JOIN pg_class c ON c.oid=indexrelid WHERE c.relname LIKE 'UX_rls_%'
 LOOP EXECUTE format('DROP INDEX %s', r.idx); END LOOP;
END $$;
""");
            migrationBuilder.Sql("""
DROP POLICY IF EXISTS tenant_isolation ON "tenant_fiscal_profiles";
DROP POLICY IF EXISTS tenant_guard ON "tenant_fiscal_profiles";
DROP POLICY IF EXISTS system_operations ON "tenant_fiscal_profiles";
ALTER TABLE "tenant_fiscal_profiles" NO FORCE ROW LEVEL SECURITY;
ALTER TABLE "tenant_fiscal_profiles" DISABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON "tenant_memberships";
DROP POLICY IF EXISTS tenant_guard ON "tenant_memberships";
DROP POLICY IF EXISTS system_operations ON "tenant_memberships";
ALTER TABLE "tenant_memberships" NO FORCE ROW LEVEL SECURITY;
ALTER TABLE "tenant_memberships" DISABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON "Customers";
DROP POLICY IF EXISTS tenant_guard ON "Customers";
DROP POLICY IF EXISTS system_operations ON "Customers";
ALTER TABLE "Customers" NO FORCE ROW LEVEL SECURITY;
ALTER TABLE "Customers" DISABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON "ServiceItems";
DROP POLICY IF EXISTS tenant_guard ON "ServiceItems";
DROP POLICY IF EXISTS system_operations ON "ServiceItems";
ALTER TABLE "ServiceItems" NO FORCE ROW LEVEL SECURITY;
ALTER TABLE "ServiceItems" DISABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON "Quotations";
DROP POLICY IF EXISTS tenant_guard ON "Quotations";
DROP POLICY IF EXISTS system_operations ON "Quotations";
ALTER TABLE "Quotations" NO FORCE ROW LEVEL SECURITY;
ALTER TABLE "Quotations" DISABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON "QuotationItem";
DROP POLICY IF EXISTS tenant_guard ON "QuotationItem";
DROP POLICY IF EXISTS system_operations ON "QuotationItem";
ALTER TABLE "QuotationItem" NO FORCE ROW LEVEL SECURITY;
ALTER TABLE "QuotationItem" DISABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON "WorkOrders";
DROP POLICY IF EXISTS tenant_guard ON "WorkOrders";
DROP POLICY IF EXISTS system_operations ON "WorkOrders";
ALTER TABLE "WorkOrders" NO FORCE ROW LEVEL SECURITY;
ALTER TABLE "WorkOrders" DISABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON "WorkOrderItems";
DROP POLICY IF EXISTS tenant_guard ON "WorkOrderItems";
DROP POLICY IF EXISTS system_operations ON "WorkOrderItems";
ALTER TABLE "WorkOrderItems" NO FORCE ROW LEVEL SECURITY;
ALTER TABLE "WorkOrderItems" DISABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON "Appointments";
DROP POLICY IF EXISTS tenant_guard ON "Appointments";
DROP POLICY IF EXISTS system_operations ON "Appointments";
ALTER TABLE "Appointments" NO FORCE ROW LEVEL SECURITY;
ALTER TABLE "Appointments" DISABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON "ReceivableTitles";
DROP POLICY IF EXISTS tenant_guard ON "ReceivableTitles";
DROP POLICY IF EXISTS system_operations ON "ReceivableTitles";
ALTER TABLE "ReceivableTitles" NO FORCE ROW LEVEL SECURITY;
ALTER TABLE "ReceivableTitles" DISABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON "ReceivablePayments";
DROP POLICY IF EXISTS tenant_guard ON "ReceivablePayments";
DROP POLICY IF EXISTS system_operations ON "ReceivablePayments";
ALTER TABLE "ReceivablePayments" NO FORCE ROW LEVEL SECURITY;
ALTER TABLE "ReceivablePayments" DISABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON "PayableTitles";
DROP POLICY IF EXISTS tenant_guard ON "PayableTitles";
DROP POLICY IF EXISTS system_operations ON "PayableTitles";
ALTER TABLE "PayableTitles" NO FORCE ROW LEVEL SECURITY;
ALTER TABLE "PayableTitles" DISABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON "PayablePayments";
DROP POLICY IF EXISTS tenant_guard ON "PayablePayments";
DROP POLICY IF EXISTS system_operations ON "PayablePayments";
ALTER TABLE "PayablePayments" NO FORCE ROW LEVEL SECURITY;
ALTER TABLE "PayablePayments" DISABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON "NotificationMessages";
DROP POLICY IF EXISTS tenant_guard ON "NotificationMessages";
DROP POLICY IF EXISTS system_operations ON "NotificationMessages";
ALTER TABLE "NotificationMessages" NO FORCE ROW LEVEL SECURITY;
ALTER TABLE "NotificationMessages" DISABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON "TenantSubscriptions";
DROP POLICY IF EXISTS tenant_guard ON "TenantSubscriptions";
DROP POLICY IF EXISTS system_operations ON "TenantSubscriptions";
ALTER TABLE "TenantSubscriptions" NO FORCE ROW LEVEL SECURITY;
ALTER TABLE "TenantSubscriptions" DISABLE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON "team_invitations";
DROP POLICY IF EXISTS tenant_guard ON "team_invitations";
DROP POLICY IF EXISTS system_operations ON "team_invitations";
ALTER TABLE "team_invitations" NO FORCE ROW LEVEL SECURITY;
ALTER TABLE "team_invitations" DISABLE ROW LEVEL SECURITY;
""");

            migrationBuilder.DropForeignKey(
                name: "FK_Appointments_tenants_tenant_id",
                table: "Appointments");

            migrationBuilder.DropForeignKey(
                name: "FK_Customers_tenants_tenant_id",
                table: "Customers");

            migrationBuilder.DropForeignKey(
                name: "FK_NotificationMessages_tenants_tenant_id",
                table: "NotificationMessages");

            migrationBuilder.DropForeignKey(
                name: "FK_PayablePayments_tenants_tenant_id",
                table: "PayablePayments");

            migrationBuilder.DropForeignKey(
                name: "FK_PayableTitles_tenants_tenant_id",
                table: "PayableTitles");

            migrationBuilder.DropForeignKey(
                name: "FK_QuotationItem_tenants_tenant_id",
                table: "QuotationItem");

            migrationBuilder.DropForeignKey(
                name: "FK_Quotations_tenants_tenant_id",
                table: "Quotations");

            migrationBuilder.DropForeignKey(
                name: "FK_ReceivablePayments_tenants_tenant_id",
                table: "ReceivablePayments");

            migrationBuilder.DropForeignKey(
                name: "FK_ReceivableTitles_tenants_tenant_id",
                table: "ReceivableTitles");

            migrationBuilder.DropForeignKey(
                name: "FK_ServiceItems_tenants_tenant_id",
                table: "ServiceItems");

            migrationBuilder.DropForeignKey(
                name: "FK_team_invitations_tenants_tenant_id",
                table: "team_invitations");

            migrationBuilder.DropForeignKey(
                name: "FK_tenant_fiscal_profiles_tenants_tenant_id",
                table: "tenant_fiscal_profiles");

            migrationBuilder.DropForeignKey(
                name: "FK_tenant_memberships_tenants_tenant_id",
                table: "tenant_memberships");

            migrationBuilder.DropForeignKey(
                name: "FK_TenantSubscriptions_tenants_tenant_id",
                table: "TenantSubscriptions");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkOrderItems_tenants_tenant_id",
                table: "WorkOrderItems");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkOrders_tenants_tenant_id",
                table: "WorkOrders");

            migrationBuilder.DropIndex(
                name: "IX_WorkOrderItems_tenant_id",
                table: "WorkOrderItems");

            migrationBuilder.DropIndex(
                name: "IX_tenant_memberships_tenant_id",
                table: "tenant_memberships");

            migrationBuilder.DropIndex(
                name: "IX_tenant_fiscal_profiles_tenant_id",
                table: "tenant_fiscal_profiles");

            migrationBuilder.DropIndex(
                name: "IX_team_invitations_tenant_id",
                table: "team_invitations");

            migrationBuilder.DropIndex(
                name: "IX_ServiceItems_tenant_id",
                table: "ServiceItems");

            migrationBuilder.DropIndex(
                name: "IX_ReceivableTitles_tenant_id",
                table: "ReceivableTitles");

            migrationBuilder.DropIndex(
                name: "IX_ReceivablePayments_tenant_id",
                table: "ReceivablePayments");

            migrationBuilder.DropIndex(
                name: "IX_Quotations_tenant_id",
                table: "Quotations");

            migrationBuilder.DropIndex(
                name: "IX_QuotationItem_tenant_id",
                table: "QuotationItem");

            migrationBuilder.DropIndex(
                name: "IX_PayableTitles_tenant_id",
                table: "PayableTitles");

            migrationBuilder.DropIndex(
                name: "IX_PayablePayments_tenant_id",
                table: "PayablePayments");

            migrationBuilder.DropIndex(
                name: "IX_NotificationMessages_tenant_id",
                table: "NotificationMessages");

            migrationBuilder.DropIndex(
                name: "IX_Customers_tenant_id",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "WorkOrderItems");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "ReceivablePayments");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "QuotationItem");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "PayablePayments");

            migrationBuilder.RenameColumn(
                name: "tenant_id",
                table: "WorkOrders",
                newName: "TenantId");

            migrationBuilder.RenameIndex(
                name: "IX_WorkOrders_tenant_id",
                table: "WorkOrders",
                newName: "IX_WorkOrders_TenantId");

            migrationBuilder.RenameColumn(
                name: "tenant_id",
                table: "TenantSubscriptions",
                newName: "TenantId");

            migrationBuilder.RenameIndex(
                name: "IX_TenantSubscriptions_tenant_id",
                table: "TenantSubscriptions",
                newName: "IX_TenantSubscriptions_TenantId");

            migrationBuilder.RenameColumn(
                name: "tenant_id",
                table: "tenant_memberships",
                newName: "TenantId");

            migrationBuilder.RenameIndex(
                name: "IX_tenant_memberships_tenant_id_UserId",
                table: "tenant_memberships",
                newName: "IX_tenant_memberships_TenantId_UserId");

            migrationBuilder.RenameColumn(
                name: "tenant_id",
                table: "tenant_fiscal_profiles",
                newName: "TenantId");

            migrationBuilder.RenameColumn(
                name: "tenant_id",
                table: "team_invitations",
                newName: "TenantId");

            migrationBuilder.RenameColumn(
                name: "tenant_id",
                table: "ServiceItems",
                newName: "TenantId");

            migrationBuilder.RenameIndex(
                name: "IX_ServiceItems_tenant_id_IsActive_Name",
                table: "ServiceItems",
                newName: "IX_ServiceItems_TenantId_IsActive_Name");

            migrationBuilder.RenameColumn(
                name: "tenant_id",
                table: "ReceivableTitles",
                newName: "TenantId");

            migrationBuilder.RenameIndex(
                name: "IX_ReceivableTitles_tenant_id_DueDate_Status",
                table: "ReceivableTitles",
                newName: "IX_ReceivableTitles_TenantId_DueDate_Status");

            migrationBuilder.RenameColumn(
                name: "tenant_id",
                table: "Quotations",
                newName: "TenantId");

            migrationBuilder.RenameIndex(
                name: "IX_Quotations_tenant_id_Status",
                table: "Quotations",
                newName: "IX_Quotations_TenantId_Status");

            migrationBuilder.RenameIndex(
                name: "IX_Quotations_tenant_id_CustomerId",
                table: "Quotations",
                newName: "IX_Quotations_TenantId_CustomerId");

            migrationBuilder.RenameIndex(
                name: "IX_Quotations_tenant_id_Code",
                table: "Quotations",
                newName: "IX_Quotations_TenantId_Code");

            migrationBuilder.RenameColumn(
                name: "tenant_id",
                table: "PayableTitles",
                newName: "TenantId");

            migrationBuilder.RenameIndex(
                name: "IX_PayableTitles_tenant_id_DueDate_Status",
                table: "PayableTitles",
                newName: "IX_PayableTitles_TenantId_DueDate_Status");

            migrationBuilder.RenameColumn(
                name: "tenant_id",
                table: "NotificationMessages",
                newName: "TenantId");

            migrationBuilder.RenameIndex(
                name: "IX_NotificationMessages_tenant_id_Status",
                table: "NotificationMessages",
                newName: "IX_NotificationMessages_TenantId_Status");

            migrationBuilder.RenameColumn(
                name: "tenant_id",
                table: "Customers",
                newName: "TenantId");

            migrationBuilder.RenameIndex(
                name: "IX_Customers_tenant_id_Phone",
                table: "Customers",
                newName: "IX_Customers_TenantId_Phone");

            migrationBuilder.RenameIndex(
                name: "IX_Customers_tenant_id_Name",
                table: "Customers",
                newName: "IX_Customers_TenantId_Name");

            migrationBuilder.RenameIndex(
                name: "IX_Customers_tenant_id_Id",
                table: "Customers",
                newName: "IX_Customers_TenantId_Id");

            migrationBuilder.RenameColumn(
                name: "tenant_id",
                table: "Appointments",
                newName: "TenantId");

            migrationBuilder.RenameIndex(
                name: "IX_Appointments_tenant_id_StartTime",
                table: "Appointments",
                newName: "IX_Appointments_TenantId_StartTime");

            migrationBuilder.RenameIndex(
                name: "IX_Appointments_tenant_id_AssignedToUserId",
                table: "Appointments",
                newName: "IX_Appointments_TenantId_AssignedToUserId");

            migrationBuilder.RenameIndex(
                name: "IX_Appointments_tenant_id",
                table: "Appointments",
                newName: "IX_Appointments_TenantId");

            migrationBuilder.AddForeignKey(
                name: "FK_tenant_fiscal_profiles_tenants_TenantId",
                table: "tenant_fiscal_profiles",
                column: "TenantId",
                principalTable: "tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_tenant_memberships_tenants_TenantId",
                table: "tenant_memberships",
                column: "TenantId",
                principalTable: "tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
