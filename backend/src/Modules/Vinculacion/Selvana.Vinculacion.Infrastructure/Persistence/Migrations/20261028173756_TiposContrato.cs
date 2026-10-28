using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Selvana.Vinculacion.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TiposContrato : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tipos_contrato",
                schema: "vinculacion",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    modalidad = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false),
                    por_defecto = table.Column<bool>(type: "boolean", nullable: false),
                    actualizado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actualizado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    plantilla_archivo_id = table.Column<Guid>(type: "uuid", nullable: true),
                    plantilla_nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    plantilla_tipo_contenido = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tipos_contrato", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_tipos_contrato_empresa_id_nombre",
                schema: "vinculacion",
                table: "tipos_contrato",
                columns: new[] { "empresa_id", "nombre" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tipos_contrato",
                schema: "vinculacion");
        }
    }
}
