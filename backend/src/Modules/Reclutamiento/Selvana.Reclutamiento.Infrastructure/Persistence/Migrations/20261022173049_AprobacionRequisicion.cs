using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Selvana.Reclutamiento.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AprobacionRequisicion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "aprobacion_pedida_por",
                schema: "reclutamiento",
                table: "vacantes",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "aprobaciones",
                schema: "reclutamiento",
                table: "vacantes",
                type: "jsonb",
                nullable: true,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "etapa_pendiente",
                schema: "reclutamiento",
                table: "vacantes",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "motivo_devolucion",
                schema: "reclutamiento",
                table: "vacantes",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "aprobacion_pedida_por",
                schema: "reclutamiento",
                table: "vacantes");

            migrationBuilder.DropColumn(
                name: "aprobaciones",
                schema: "reclutamiento",
                table: "vacantes");

            migrationBuilder.DropColumn(
                name: "etapa_pendiente",
                schema: "reclutamiento",
                table: "vacantes");

            migrationBuilder.DropColumn(
                name: "motivo_devolucion",
                schema: "reclutamiento",
                table: "vacantes");
        }
    }
}
