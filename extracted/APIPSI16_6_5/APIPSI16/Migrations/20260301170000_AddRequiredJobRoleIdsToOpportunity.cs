using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace APIPSI16.Migrations
{
    /// <inheritdoc />
    public partial class AddRequiredJobRoleIdsToOpportunity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RequiredJobRoleIds",
                table: "Opportunities",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RequiredJobRoleIds",
                table: "Opportunities");
        }
    }
}
