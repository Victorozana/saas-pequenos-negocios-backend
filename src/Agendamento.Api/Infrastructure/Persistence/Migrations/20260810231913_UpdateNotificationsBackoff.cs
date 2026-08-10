using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agendamento.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UpdateNotificationsBackoff : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "NextRetryAt",
                table: "NotificationMessages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotificationMessages_Status_NextRetryAt_CreatedAt",
                table: "NotificationMessages",
                columns: new[] { "Status", "NextRetryAt", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_NotificationMessages_Status_NextRetryAt_CreatedAt",
                table: "NotificationMessages");

            migrationBuilder.DropColumn(
                name: "NextRetryAt",
                table: "NotificationMessages");
        }
    }
}
