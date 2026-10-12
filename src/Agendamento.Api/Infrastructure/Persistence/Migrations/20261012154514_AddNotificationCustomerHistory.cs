using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agendamento.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationCustomerHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Audience",
                table: "NotificationMessages",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "LegacyUnlinked");

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                table: "NotificationMessages",
                type: "uuid",
                nullable: true);

            // Existing rows have no trustworthy customer provenance. Preserve every field
            // and explicitly classify them; new application writes must supply an audience.
            migrationBuilder.Sql("ALTER TABLE \"NotificationMessages\" ALTER COLUMN \"Audience\" DROP DEFAULT");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Customers_tenant_id_Id",
                table: "Customers",
                columns: new[] { "tenant_id", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationMessages_tenant_id_CustomerId_CreatedAt",
                table: "NotificationMessages",
                columns: new[] { "tenant_id", "CustomerId", "CreatedAt" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_NotificationMessages_AudienceCustomer",
                table: "NotificationMessages",
                sql: "(\"Audience\" = 'Customer' AND \"CustomerId\" IS NOT NULL) OR (\"Audience\" IN ('Standalone', 'System', 'LegacyUnlinked') AND \"CustomerId\" IS NULL)");

            migrationBuilder.AddForeignKey(
                name: "FK_NotificationMessages_Customers_tenant_id_CustomerId",
                table: "NotificationMessages",
                columns: new[] { "tenant_id", "CustomerId" },
                principalTable: "Customers",
                principalColumns: new[] { "tenant_id", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_NotificationMessages_Customers_tenant_id_CustomerId",
                table: "NotificationMessages");

            migrationBuilder.DropIndex(
                name: "IX_NotificationMessages_tenant_id_CustomerId_CreatedAt",
                table: "NotificationMessages");

            migrationBuilder.DropCheckConstraint(
                name: "CK_NotificationMessages_AudienceCustomer",
                table: "NotificationMessages");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Customers_tenant_id_Id",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "Audience",
                table: "NotificationMessages");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                table: "NotificationMessages");
        }
    }
}
