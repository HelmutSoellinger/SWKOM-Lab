using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DMSystem.DAL.Migrations
{
    /// <inheritdoc />
    public partial class RemoveDescriptionColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Description",
                table: "Documents");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Documents",
                type: "text",
                nullable: true);
        }
    }
}
