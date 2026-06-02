using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkflowAutomation.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAutomationRunIsTest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsTest",
                table: "AutomationRuns",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsTest",
                table: "AutomationRuns");
        }
    }
}
