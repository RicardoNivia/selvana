using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Selvana.Reclutamiento.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TableroPostulaciones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "etapa",
                schema: "reclutamiento",
                table: "postulaciones",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Recibida");

            migrationBuilder.AddColumn<string>(
                name: "historial",
                schema: "reclutamiento",
                table: "postulaciones",
                type: "jsonb",
                nullable: true,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "motivo_descarte",
                schema: "reclutamiento",
                table: "postulaciones",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "etapa",
                schema: "reclutamiento",
                table: "postulaciones");

            migrationBuilder.DropColumn(
                name: "historial",
                schema: "reclutamiento",
                table: "postulaciones");

            migrationBuilder.DropColumn(
                name: "motivo_descarte",
                schema: "reclutamiento",
                table: "postulaciones");
        }
    }
}
