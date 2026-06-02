using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkflowAutomation.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAutomationTriggerConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TriggerConfig",
                table: "Automations",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TriggerConfig",
                table: "Automations");
        }
    }
}
