using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Selvana.Reclutamiento.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReglasAvance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "reglas_avance",
                schema: "reclutamiento",
                table: "vacantes",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "reglas_avance",
                schema: "reclutamiento",
                table: "vacantes");
        }
    }
}
