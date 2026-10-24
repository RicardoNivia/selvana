using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Selvana.Reclutamiento.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SuitePsicotecnica : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "pruebas",
                schema: "reclutamiento",
                table: "vacantes",
                type: "jsonb",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "aplicaciones_pruebas",
                schema: "reclutamiento",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    vacante_id = table.Column<Guid>(type: "uuid", nullable: false),
                    postulacion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    candidato_id = table.Column<Guid>(type: "uuid", nullable: false),
                    titular = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    token_huella = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    enviada_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    enviada_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    vence_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    consentimiento_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    terminada_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cambios_de_pestana = table.Column<int>(type: "integer", nullable: false),
                    pruebas = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_aplicaciones_pruebas", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "baterias_cargo",
                schema: "reclutamiento",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    clave_cargo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    cargo_nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    actualizada_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    actualizada_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    pruebas = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_baterias_cargo", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_aplicaciones_pruebas_empresa_id_postulacion_id",
                schema: "reclutamiento",
                table: "aplicaciones_pruebas",
                columns: new[] { "empresa_id", "postulacion_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_aplicaciones_pruebas_empresa_id_vacante_id",
                schema: "reclutamiento",
                table: "aplicaciones_pruebas",
                columns: new[] { "empresa_id", "vacante_id" });

            migrationBuilder.CreateIndex(
                name: "ix_aplicaciones_pruebas_token_huella",
                schema: "reclutamiento",
                table: "aplicaciones_pruebas",
                column: "token_huella",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_baterias_cargo_empresa_id_clave_cargo",
                schema: "reclutamiento",
                table: "baterias_cargo",
                columns: new[] { "empresa_id", "clave_cargo" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "aplicaciones_pruebas",
                schema: "reclutamiento");

            migrationBuilder.DropTable(
                name: "baterias_cargo",
                schema: "reclutamiento");

            migrationBuilder.DropColumn(
                name: "pruebas",
                schema: "reclutamiento",
                table: "vacantes");
        }
    }
}
