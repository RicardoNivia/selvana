using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Selvana.Reclutamiento.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PreseleccionConversacional : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "conversaciones_preseleccion",
                schema: "reclutamiento",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_usuaria_id = table.Column<Guid>(type: "uuid", nullable: true),
                    vacante_id = table.Column<Guid>(type: "uuid", nullable: false),
                    postulacion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    candidato_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    pensando = table.Column<bool>(type: "boolean", nullable: false),
                    motivo_escalamiento = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    modelo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    invitada_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    creada_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actualizada_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    mensajes = table.Column<string>(type: "jsonb", nullable: true),
                    resumen = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_conversaciones_preseleccion", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_conversaciones_preseleccion_empresa_id_postulacion_id",
                schema: "reclutamiento",
                table: "conversaciones_preseleccion",
                columns: new[] { "empresa_id", "postulacion_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_conversaciones_preseleccion_token_hash",
                schema: "reclutamiento",
                table: "conversaciones_preseleccion",
                column: "token_hash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "conversaciones_preseleccion",
                schema: "reclutamiento");
        }
    }
}
