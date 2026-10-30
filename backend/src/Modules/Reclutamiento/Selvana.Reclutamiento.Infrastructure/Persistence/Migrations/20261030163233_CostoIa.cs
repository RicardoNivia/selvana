using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Selvana.Reclutamiento.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CostoIa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "costo_ia_usd",
                schema: "reclutamiento",
                table: "vacantes",
                type: "numeric(12,6)",
                precision: 12,
                scale: 6,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "costo_ia_usd",
                schema: "reclutamiento",
                table: "postulaciones",
                type: "numeric(12,6)",
                precision: 12,
                scale: 6,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "costo_ia_usd",
                schema: "reclutamiento",
                table: "conversaciones_preseleccion",
                type: "numeric(12,6)",
                precision: 12,
                scale: 6,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "costo_ia_usd",
                schema: "reclutamiento",
                table: "vacantes");

            migrationBuilder.DropColumn(
                name: "costo_ia_usd",
                schema: "reclutamiento",
                table: "postulaciones");

            migrationBuilder.DropColumn(
                name: "costo_ia_usd",
                schema: "reclutamiento",
                table: "conversaciones_preseleccion");
        }
    }
}
