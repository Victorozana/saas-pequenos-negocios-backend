using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agendamento.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerAutomaticReminderPreference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AutomaticRemindersEnabled",
                table: "Customers",
                type: "boolean",
                nullable: false,
                defaultValue: true);
            migrationBuilder.Sql("ALTER TABLE \"Customers\" ALTER COLUMN \"AutomaticRemindersEnabled\" DROP DEFAULT");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AutomaticRemindersEnabled",
                table: "Customers");
        }
    }
}
