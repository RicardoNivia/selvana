using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Selvana.Core.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BandejaDeAprobaciones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "propuestas_bandeja",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_usuaria_id = table.Column<Guid>(type: "uuid", nullable: true),
                    modulo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    tipo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    entidad_id = table.Column<Guid>(type: "uuid", nullable: false),
                    titulo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    agente = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    modelo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    revisar = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    afecta_persona = table.Column<bool>(type: "boolean", nullable: false),
                    vence_el = table.Column<DateOnly>(type: "date", nullable: true),
                    creada_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actualizada_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    decision = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    decidida_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    decidida_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    accion = table.Column<string>(type: "jsonb", nullable: true),
                    campos = table.Column<string>(type: "jsonb", nullable: true),
                    razones = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_propuestas_bandeja", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_propuestas_bandeja_empresa_id_estado",
                schema: "core",
                table: "propuestas_bandeja",
                columns: new[] { "empresa_id", "estado" });

            migrationBuilder.CreateIndex(
                name: "ix_propuestas_bandeja_empresa_id_tipo_entidad_id",
                schema: "core",
                table: "propuestas_bandeja",
                columns: new[] { "empresa_id", "tipo", "entidad_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "propuestas_bandeja",
                schema: "core");
        }
    }
}
