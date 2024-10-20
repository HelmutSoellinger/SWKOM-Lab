using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DMSystem.DAL.Migrations
{
    public partial class PdfFormatMigration : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Alter the Content column to bytea with explicit casting
            migrationBuilder.Sql("ALTER TABLE \"Documents\" ALTER COLUMN \"Content\" TYPE bytea USING \"Content\"::bytea;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reverse the migration by changing the Content column back to text
            migrationBuilder.Sql("ALTER TABLE \"Documents\" ALTER COLUMN \"Content\" TYPE text USING \"Content\"::text;");
        }
    }
}
