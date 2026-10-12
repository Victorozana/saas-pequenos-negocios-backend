using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agendamento.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDailyReceivableReminderDeduplication : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Trusted daily producer needs reads and row locks, never financial writes.
            migrationBuilder.Sql("""
                GRANT SELECT ON "ReceivableTitles", "ReceivablePayments" TO agendamento_system_user;
                GRANT UPDATE ("Id") ON "ReceivableTitles" TO agendamento_system_user;
                CREATE POLICY system_daily_reminder_read ON "ReceivableTitles" FOR SELECT
                    TO agendamento_system_user USING (true);
                CREATE POLICY system_daily_reminder_lock ON "ReceivableTitles" FOR UPDATE
                    TO agendamento_system_user USING (true) WITH CHECK (false);
                CREATE POLICY system_daily_reminder_read ON "ReceivablePayments" FOR SELECT
                    TO agendamento_system_user USING (true);
                """);
            migrationBuilder.AddColumn<Guid>(
                name: "ReceivableTitleId",
                table: "NotificationMessages",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ReminderDate",
                table: "NotificationMessages",
                type: "date",
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_ReceivableTitles_tenant_id_Id",
                table: "ReceivableTitles",
                columns: new[] { "tenant_id", "Id" });

            migrationBuilder.CreateIndex(
                name: "UX_NotificationMessages_DailyReceivableReminder",
                table: "NotificationMessages",
                columns: new[] { "tenant_id", "ReceivableTitleId", "ReminderDate" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_NotificationMessages_DailyReceivableReminder",
                table: "NotificationMessages",
                sql: "(\"ReceivableTitleId\" IS NULL AND \"ReminderDate\" IS NULL) OR (\"ReceivableTitleId\" IS NOT NULL AND \"ReminderDate\" IS NOT NULL AND \"Audience\" = 'Customer')");

            migrationBuilder.AddForeignKey(
                name: "FK_NotificationMessages_ReceivableTitles_tenant_id_ReceivableT~",
                table: "NotificationMessages",
                columns: new[] { "tenant_id", "ReceivableTitleId" },
                principalTable: "ReceivableTitles",
                principalColumns: new[] { "tenant_id", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP POLICY system_daily_reminder_read ON "ReceivableTitles";
                DROP POLICY system_daily_reminder_lock ON "ReceivableTitles";
                DROP POLICY system_daily_reminder_read ON "ReceivablePayments";
                REVOKE SELECT ON "ReceivableTitles", "ReceivablePayments" FROM agendamento_system_user;
                REVOKE UPDATE ("Id") ON "ReceivableTitles" FROM agendamento_system_user;
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_NotificationMessages_ReceivableTitles_tenant_id_ReceivableT~",
                table: "NotificationMessages");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_ReceivableTitles_tenant_id_Id",
                table: "ReceivableTitles");

            migrationBuilder.DropIndex(
                name: "UX_NotificationMessages_DailyReceivableReminder",
                table: "NotificationMessages");

            migrationBuilder.DropCheckConstraint(
                name: "CK_NotificationMessages_DailyReceivableReminder",
                table: "NotificationMessages");

            migrationBuilder.DropColumn(
                name: "ReceivableTitleId",
                table: "NotificationMessages");

            migrationBuilder.DropColumn(
                name: "ReminderDate",
                table: "NotificationMessages");
        }
    }
}
